using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Consistency;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf.Grpc;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class ConsistencyService<TCrdt, TImplementation, TRepresentation, TDto> : BackgroundService
        where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
        where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
    {
        private readonly ManagedCrdtContext _context;
        private readonly IChannelManager _channelManager;
        private readonly IConsistencyCheckTargetsSelectionStrategy _strategy;
        private readonly NodeId _thisNodeId;
        private readonly TimeSpan _delayBetweenChecks = TimeSpan.FromSeconds(60);

        private readonly ILogger<ConsistencyService<TCrdt, TImplementation, TRepresentation, TDto>> _logger;

        /// <inheritdoc />
        public ConsistencyService(ManagedCrdtContext context,
            IChannelManager channelManager,
            IConsistencyCheckTargetsSelectionStrategy strategy,
            NodeInfo thisNode,
            ILogger<ConsistencyService<TCrdt, TImplementation, TRepresentation, TDto>> logger)
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
            var typeName = TypeNameCompressor.GetName<TCrdt>();

            foreach (var instanceId in _context.GetInstanceIds<TCrdt>())
            {
                var nodesWithReplica = _context.GetNodesThatHaveReplica<TCrdt>(instanceId);
                foreach (var nodeId in _strategy.GetTargetNodes(nodesWithReplica, _thisNodeId))
                {
                    _logger.LogDebug("Executing consistency check for {CrdtType} with node {NodeId}",
                        typeof(TCrdt), nodeId);
                    if (!_channelManager.TryGet<IDtoPassingGrpcService<TDto>>(nodeId, out var client))
                    {
                        _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
                        continue;
                    }

                    var nameAndInstanceId = new TypeNameAndInstanceId(typeName, instanceId);
                    var hash = await client.GetHashAsync(nameAndInstanceId, cancellationToken);
                    var hashMsg = new TypeNameAndHash(typeName, hash).WithId(instanceId);

                    if (_context.IsHashEqual(hashMsg)) continue;

                    _logger.LogInformation("Hash {MyHash} of {CrdtType} with id {CrdtInstanceId} does not " +
                                           "match with local one",
                        hash, typeName, instanceId);
                    await SyncCrdtsAsync(client, typeName, instanceId, cancellationToken);
                }
            }
        }

        private async Task SyncCrdtsAsync(IDtoPassingGrpcService<TDto> dtoPassingGrpcService, string typeName, string instanceId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Syncing CRDT of type {CrdtType} with instanceId {InstanceId}",
                typeof(TCrdt), instanceId);

            var enumerable = _context.EnumerateDtoBatchesAsync<TCrdt, TImplementation, TRepresentation, TDto>(instanceId, cancellationToken);
            var callOptions = new CallOptions(new Metadata
            {
                new("instance-id", instanceId),
                new("crdt-type-name", typeName)
            }, cancellationToken: cancellationToken);
            await foreach (var dto in dtoPassingGrpcService.EnumerateCrdtAsync(enumerable, callOptions).WithCancellation(cancellationToken))
            {
                await _context.MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(dto, instanceId, cancellationToken);
            }
        }
    }
}