using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Propagation;
using Nyris.Crdt.Distributed.Utils;

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

        /// <inheritdoc />
        public PropagationService(ManagedCrdtContext context,
            IPropagationStrategy propagationStrategy,
            IChannelManager channelManager,
            IAsyncQueueProvider queueProvider,
            NodeInfo thisNode,
            ILogger<PropagationService<TCrdt, TDto>> logger)
        {
            _context = context;
            _propagationStrategy = propagationStrategy;
            _channelManager = channelManager;
            _queueProvider = queueProvider;
            _thisNodeId = thisNode.Id;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queue = _queueProvider.GetQueue<TDto>(typeof(TCrdt));
            _logger.LogInformation("Consuming dto queue for crdt '{CrdtType}' with dto type '{DtoType}'",
                typeof(TCrdt), typeof(TDto));
            await foreach (var dto in queue.WithCancellation(stoppingToken))
            {
                _logger.LogDebug("TraceId: {TraceId}, Preparing to send dto {Dto}. \nQueue size: {QueueSize}",
                    dto.TraceId, JsonConvert.SerializeObject(dto), queue.QueueLength);
                try
                {
                    var nodesWithReplica = _context
                        .GetNodesThatHaveReplica(new TypeNameAndInstanceId(dto.TypeName, dto.InstanceId))
                        .ToList();

                    _logger.LogDebug("TraceId: {TraceId}, context yielded the following nodes with replica: {Nodes}",
                        dto.TraceId, string.Join("; ", nodesWithReplica.Select(ni => $"{ni.Id}:{ni.Address}")));

                    foreach (var nodeId in _propagationStrategy.GetTargetNodes(nodesWithReplica, _thisNodeId))
                    {
                        if (!_channelManager.TryGet<IDtoPassingGrpcService<TDto>>(nodeId, out var client))
                        {
                            _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                            continue;
                        }

                        var response = await client.SendAsync(dto, stoppingToken);

                        _logger.LogDebug("TraceId: {TraceId}, Received back dto {Dto}",
                            dto.TraceId, JsonConvert.SerializeObject(response));

                        // we should NOT await merge here, because on conflict it will try to
                        // publish dto to the queue, which might be full. And since this loop
                        // is the only way to free the queue, we have a deadlock
                        _ = _context.MergeAsync<TCrdt, TDto>(response,
                            dto.InstanceId,
                            traceId: dto.TraceId,
                            cancellationToken: stoppingToken);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unhandled exception during sending a dto");
                }
                finally
                {
                    _logger.LogDebug("TraceId: {TraceId}, releasing dto", dto.TraceId);
                    dto.Complete();
                }
            }

            _logger.LogError("Queue in {ServiceName} finished enumerating, which is unexpected", GetType().Name);
        }
    }
}
