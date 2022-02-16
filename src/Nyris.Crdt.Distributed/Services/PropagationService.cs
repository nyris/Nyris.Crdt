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

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class PropagationService<TCrdt, TDto> : BackgroundService
        where TCrdt : ManagedCRDT<TDto>
    {
        private readonly ManagedCrdtContext _context;
        private readonly IPropagationStrategy _propagationStrategy;
        private readonly IChannelManager _channelManager;
        private readonly ILogger<PropagationService<TCrdt, TDto>> _logger;
        private readonly NodeId _thisNodeId;

        /// <inheritdoc />
        public PropagationService(ManagedCrdtContext context,
            ILogger<PropagationService<TCrdt, TDto>> logger,
            NodeInfo thisNode,
            IPropagationStrategy propagationStrategy,
            IChannelManager channelManager)
        {
            _context = context;
            _logger = logger;
            _thisNodeId = thisNode.Id;
            _propagationStrategy = propagationStrategy;
            _channelManager = channelManager;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queue = Queues.GetQueue<TDto>(typeof(TCrdt));
            _logger.LogInformation("Consuming dto queue for crdt '{CrdtType}' with dto type '{DtoType}'",
                typeof(TCrdt), typeof(TDto));
            await foreach (var dto in queue)
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
                        await _context.MergeAsync<TCrdt, TDto>(response, dto.InstanceId, cancellationToken: stoppingToken);
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
