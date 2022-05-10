using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Propagation;
using Nyris.Crdt.Distributed.Utils;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class PropagationService<TCrdt, TDto> : BackgroundService
        where TCrdt : ManagedCRDT<TDto>
    {
        private readonly ManagedCrdtContext _context;
        private readonly IPropagationStrategy _propagationStrategy;
        private readonly IChannelManager _channelManager;
        private readonly IAsyncQueueProvider _queueProvider;
        private readonly NodeId _thisNodeId;
        private readonly ILogger<PropagationService<TCrdt, TDto>> _logger;
        private readonly IHostApplicationLifetime _lifetime;

        /// <inheritdoc />
        public PropagationService(
            ManagedCrdtContext context,
            IPropagationStrategy propagationStrategy,
            IChannelManager channelManager,
            IAsyncQueueProvider queueProvider,
            NodeInfo thisNode,
            IHostApplicationLifetime lifetime,
            ILogger<PropagationService<TCrdt, TDto>> logger
        )
        {
            _context = context;
            _propagationStrategy = propagationStrategy;
            _channelManager = channelManager;
            _queueProvider = queueProvider;
            _lifetime = lifetime;
            _thisNodeId = thisNode.Id;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queue = _queueProvider.GetQueue<TDto>(typeof(TCrdt));
            _logger.LogInformation("Consuming dto queue for crdt '{CrdtType}' with dto type '{DtoType}'",
                                   typeof(TCrdt), typeof(TDto));

            // Length of the array specifies how many tasks can be executed in parallel
            var tasks = new Task[4];
            // completionBuffer is a queue for finished tasks (for indexes in array that can be used for the next task)
            var completionBuffer = new BufferBlock<int>();

            // at first, all indexes in array are available.
            for (var i = 0; i < tasks.Length; ++i)
            {
                completionBuffer.Post(i);
            }

            await foreach (var dto in queue.WithCancellation(stoppingToken))
            {
                // await first finished task, add new one in it's place
                var nextTaskPlace = await completionBuffer.ReceiveAsync(stoppingToken);
                tasks[nextTaskPlace] = ProcessDtoMessage(dto, stoppingToken).ContinueWith(
                                                                                          _ => { completionBuffer.Post(nextTaskPlace); },
                                                                                          stoppingToken,
                                                                                          TaskContinuationOptions.ExecuteSynchronously,
                                                                                          TaskScheduler.Current);
            }

            _logger.LogError("Queue in {ServiceName} finished enumerating, which is unexpected", GetType().Name);
            _lifetime.StopApplication();
        }

        private async Task ProcessDtoMessage(DtoMessage<TDto> dto, CancellationToken cancellationToken)
        {
            try
            {
                var nodesWithReplica = _context
                                       .GetNodesThatHaveReplica(new TypeNameAndInstanceId(dto.TypeName, dto.InstanceId))
                                       .ToList();

                // _logger.LogDebug("TraceId: {TraceId}, context yielded the following nodes with replica: {Nodes}",
                //     dto.TraceId, string.Join("; ", nodesWithReplica.Select(ni => $"{ni.Id}:{ni.Address}")));

                foreach (var nodeId in _propagationStrategy.GetTargetNodes(nodesWithReplica, _thisNodeId))
                {
                    if (!_channelManager.TryGet<IDtoPassingGrpcService<TDto>>(nodeId, out var client))
                    {
                        _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                        continue;
                    }

                    var response = await client.SendAsync(dto, cancellationToken);

                    // _logger.LogDebug("TraceId: {TraceId}, Received back dto {Dto}",
                    //     dto.TraceId, JsonConvert.SerializeObject(response));

                    await _context.MergeAsync<TCrdt, TDto>(response,
                                                           dto.InstanceId,
                                                           traceId: dto.TraceId,
                                                           allowPropagation: false,
                                                           cancellationToken: cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception during sending a dto");
            }
            finally
            {
                // _logger.LogDebug("TraceId: {TraceId}, releasing dto", dto.TraceId);
                dto.Complete();
            }
        }
    }
}
