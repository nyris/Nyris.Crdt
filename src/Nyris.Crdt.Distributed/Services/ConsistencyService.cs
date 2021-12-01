using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Exceptions;
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
        private readonly TimeSpan _delayBetweenChecks = TimeSpan.FromSeconds(60);

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
            _logger.LogDebug("Executing for {CrdtType}", typeof(TCrdt));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryHandleConsistencyCheckAsync(stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unhandled exception for Crdt of type {CrdtType}", typeof(TCrdt));
                }

                await Task.Delay(_delayBetweenChecks, stoppingToken);
            }
        }

        private async Task TryHandleConsistencyCheckAsync(CancellationToken cancellationToken)
        {
            string typeName;
            try
            {
                typeName = _context.GetTypeName<TCrdt>();
            }
            catch (ManagedCrdtContextSetupException e)
            {
                // TODO: can this be done better? Th idea is - some CRDT type can be instantiated later in the runtime
                return;
            }

            foreach (var nodeId in _strategy.GetTargetNodes(_context.Nodes.Value, _thisNodeId))
            {
                _logger.LogDebug("Executing consistency check with node {NodeId}", nodeId);
                if (!_channelManager.TryGetProxy<TDto>(nodeId, out var proxy))
                {
                    _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                    continue;
                }

                await foreach (var (hash, instanceId) in proxy.GetHashesAsync(typeName).WithCancellation(cancellationToken))
                {
                    var hashMsg = new TypeNameAndHash(typeName, hash).WithId(instanceId);
                    if (_context.IsHashEqual(hashMsg)) continue;

                    _logger.LogInformation("Hash {MyHash} of {CrdtType} with id {CrdtInstanceId} does not " +
                                           "match with local one",
                        hash, typeName, instanceId);
                    await SyncCrdtsAsync(proxy, instanceId, cancellationToken);
                }
            }
        }

        private async Task SyncCrdtsAsync(IProxy<TDto> proxy, string instanceId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Syncing CRDT of type {CrdtType} with instanceId {InstanceId}",
                typeof(TCrdt), instanceId);

            var enumerable = _context.EnumerateDtoBatchesAsync<TCrdt, TImplementation, TRepresentation, TDto>(instanceId);
            await foreach (var dto in proxy.EnumerateCrdtAsync(enumerable).WithCancellation(cancellationToken))
            {
                await _context.MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(dto);
            }
        }
    }
}