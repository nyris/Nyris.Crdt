using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Consistency;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class ConsistencyService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto> : BackgroundService
        where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        where TGrpcService : class
    {
        private readonly ManagedCrdtContext _context;
        private readonly ChannelManager<TGrpcService> _channelManager;
        private readonly IConsistencyCheckTargetsSelectionStrategy _strategy;
        private readonly NodeId _thisNodeId;

        private readonly ILogger<ConsistencyService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>> _logger;

        /// <inheritdoc />
        public ConsistencyService(ManagedCrdtContext context,
            ChannelManager<TGrpcService> channelManager,
            IConsistencyCheckTargetsSelectionStrategy strategy,
            NodeInfo thisNode,
            ILogger<ConsistencyService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>> logger)
        {
            _context = context;
            _channelManager = channelManager;
            _strategy = strategy;
            _logger = logger;
            _thisNodeId = thisNode.Id;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryHandleConsistencyCheck(stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError("Unhandled exception in {ServiceName} for Crdt of type {CrdtType}: {Exception}",
                        typeof(ConsistencyService<,,,,>), typeof(TCrdt), e.ToString());
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task TryHandleConsistencyCheck(CancellationToken cancellationToken)
        {
            var typeName = _context.GetTypeName<TCrdt>();

            foreach (var nodeId in _strategy.GetTargetNodes(_context.Nodes.Value, _thisNodeId))
            {
                if (!_channelManager.TryGetProxy<TDto>(nodeId, out var proxy))
                {
                    _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                    continue;
                }

                await foreach (var (hash, instanceId) in proxy.GetHashesAsync(typeName).WithCancellation(cancellationToken))
                {
                    var hashesMatch = await _context.IsHashEqual(new TypeNameAndHash(typeName, hash).WithId(instanceId));

                    if (!hashesMatch) await SyncCrdtsAsync(proxy, instanceId, cancellationToken);
                }
            }
        }

        private async Task SyncCrdtsAsync(IProxy<TDto> proxy, string instanceId, CancellationToken cancellationToken)
        {
            var enumerable = _context.EnumerateDtoBatchesAsync<TCrdt, TImplementation, TRepresentation, TDto>(instanceId);
            await foreach (var dto in proxy.EnumerateCrdtAsync(enumerable).WithCancellation(cancellationToken))
            {
                await _context.MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(dto);
            }
        }
    }
}