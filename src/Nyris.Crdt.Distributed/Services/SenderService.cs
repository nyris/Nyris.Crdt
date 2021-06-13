using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Propagation;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class SenderService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto> : BackgroundService
        where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        where TGrpcService : class
    {
        private readonly ManagedCrdtContext _context;
        private readonly IPropagationStrategy _propagationStrategy;
        private readonly ChannelManager<TGrpcService> _channelManager;
        private readonly ILogger<SenderService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>> _logger;
        private readonly NodeId _thisNodeId;

        /// <inheritdoc />
        public SenderService(ManagedCrdtContext context,
            ILogger<SenderService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>> logger,
            NodeInfo thisNode,
            IPropagationStrategy propagationStrategy,
            ChannelManager<TGrpcService> channelManager)
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
            await foreach (var dto in Queues.GetQueue<TDto>(typeof(TCrdt)))
            {
                try
                {
                    foreach (var nodeId in _propagationStrategy.GetTargetNodes(_context.Nodes.Value, _thisNodeId))
                    {
                        if (!_channelManager.TryGetProxy<TDto>(nodeId, out var proxy))
                        {
                            _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                            continue;
                        }

                        var response = await proxy.SendAsync(dto);
                        await _context.MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(response.WithId(dto.Id));
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Unhandled exception during sending a dto: {Exception}", e.ToString());
                }
            }
        }
    }
}
