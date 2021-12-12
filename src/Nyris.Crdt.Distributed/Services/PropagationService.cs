using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Propagation;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class PropagationService<TCrdt, TImplementation, TRepresentation, TDto> : BackgroundService
        where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
        where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
    {
        private readonly ManagedCrdtContext _context;
        private readonly IPropagationStrategy _propagationStrategy;
        private readonly IChannelManager _channelManager;
        private readonly ILogger<PropagationService<TCrdt, TImplementation, TRepresentation, TDto>> _logger;
        private readonly NodeId _thisNodeId;

        /// <inheritdoc />
        public PropagationService(ManagedCrdtContext context,
            ILogger<PropagationService<TCrdt, TImplementation, TRepresentation, TDto>> logger,
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
                _logger.LogDebug("Preparing to send dto {Dto}. \nQueue size: {QueueSize}",
                    JsonConvert.SerializeObject(dto), queue.QueueLength);
                try
                {
                    var nodesWithReplica = _context.GetNodesThatHaveReplica<TCrdt>(dto.Id);
                    foreach (var nodeId in _propagationStrategy.GetTargetNodes(nodesWithReplica, _thisNodeId))
                    {
                        if (!_channelManager.TryGet<IDtoPassingGrpcService<TDto>>(nodeId, out var client))
                        {
                            _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                            continue;
                        }

                        var response = await client.SendAsync(dto, stoppingToken);

                        _logger.LogDebug("Received back dto {Dto}",JsonConvert.SerializeObject(response));
                        await _context.MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(response, dto.Id, stoppingToken);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unhandled exception during sending a dto");
                }
            }

            _logger.LogError("Queue in {ServiceName} finished enumerating, which is unexpected", GetType().Name);
        }
    }
}
