using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

internal sealed class SynchronizationAndRelocationService : BackgroundService
{
    private static readonly TimeSpan DelayTime = TimeSpan.FromMinutes(1);

    private readonly ILogger<SynchronizationAndRelocationService> _logger;
    private readonly IClusterMetadataManager _metadata;
    private readonly INodeClientPool _clientPool;
    private readonly INodeSelectionStrategy _nodeSelectionStrategy;
    private readonly IManagedCrdtProvider _crdts;
    private readonly IReplicaDistributor _replicaDistributor;

    public SynchronizationAndRelocationService(ILogger<SynchronizationAndRelocationService> logger,
        IClusterMetadataManager metadata,
        INodeClientPool clientPool,
        INodeSelectionStrategy nodeSelectionStrategy,
        IManagedCrdtProvider crdts,
        IReplicaDistributor replicaDistributor)
    {
        _logger = logger;
        _metadata = metadata;
        _clientPool = clientPool;
        _nodeSelectionStrategy = nodeSelectionStrategy;
        _crdts = crdts;
        _replicaDistributor = replicaDistributor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(DelayTime, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SingleRunAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Sync could not finish due to an unhandled exception. Next try in {Delay}",
                    DelayTime);
            }
            
            await Task.Delay(DelayTime, stoppingToken);
        }
    }

    private async Task SingleRunAsync(CancellationToken cancellationToken)
    {
        var start = DateTime.Now;
        await SyncMetadataAsync(cancellationToken);
        await SyncManagedCrdtsAsync(cancellationToken);
        _logger.LogDebug("Sync run finished in {Duration}, next run in {Delay}", 
            DateTime.Now - start, DelayTime);
        
        // TODO: (1) remove local shards that should not be here
        // TODO: (2) upsert local shards that should be here into read replicas
    }

    private async Task SyncManagedCrdtsAsync(CancellationToken cancellationToken)
    {
        foreach (var instanceId in _crdts.InstanceIds)
        {
            if (!_crdts.TryGet(instanceId, out var crdt)) continue;

            foreach (var shardId in crdt.Shards)
            {
                // _logger.LogDebug("Starting to sync crdt ({InstanceId}, {ShardId})", instanceId, shardId);
                var nodesWithReplica = _replicaDistributor.GetNodesWithWriteReplicas(instanceId, shardId);
                var nodesToSync = _nodeSelectionStrategy.SelectNodes(nodesWithReplica);
                var timestamp = crdt.GetCausalTimestamp(shardId);
                
                foreach (var node in nodesToSync)
                {
                    // _logger.LogDebug("Starting to sync crdt ({InstanceId}, {ShardId}) with node '{NodeId}'", 
                        // instanceId, shardId, node.Id.ToString());
                    var start = DateTime.Now;
                    
                    var client = _clientPool.GetClient(node);
                    using var duplexStream = client.GetDeltaDuplexStream();
                    var otherTimestamp = await ExchangeCausalTimestampsAsync(duplexStream, instanceId, shardId, timestamp, cancellationToken);
                    await ExchangeDeltasAsync(duplexStream, crdt, shardId, otherTimestamp, cancellationToken);
                    
                    _logger.LogDebug("Syncing of crdt ({InstanceId}, {ShardId}) with node '{NodeId}' finished in {Duration}", 
                        instanceId, shardId, node.Id, DateTime.Now - start);
                }
            }
        }
    }

    private async Task ExchangeDeltasAsync(IDuplexDeltasStream duplexStream, ManagedCrdt crdt, ShardId shardId,
        ReadOnlyMemory<byte> otherTimestamp, CancellationToken cancellationToken)
    {
        var task = ConsumeDeltasFromOtherNodeAsync(duplexStream, crdt, shardId, cancellationToken);
        var enumerable = crdt.EnumerateDeltaBatchesAsync(shardId, otherTimestamp, cancellationToken);
        await duplexStream.SendDeltasAndFinishAsync(enumerable, cancellationToken);
        await task;
    }

    private async Task<ReadOnlyMemory<byte>> ExchangeCausalTimestampsAsync(IDuplexDeltasStream duplexStream, InstanceId instanceId,
        ShardId shardId, ReadOnlyMemory<byte> timestamp, CancellationToken cancellationToken)
    {
        var timestampsExchangeStart = DateTime.Now;
        var otherTimestamp = await duplexStream.ExchangeTimestampsAsync(instanceId, shardId, timestamp, cancellationToken);
        _logger.LogDebug("Exchanged timestamps for ({InstanceId}, {ShardId}) in {Duration}",
            instanceId, shardId, DateTime.Now - timestampsExchangeStart);
        return otherTimestamp;
    }

    private async Task SyncMetadataAsync(CancellationToken cancellationToken)
    {
        // _logger.LogDebug("Starting to sync node metadata");
        var nodes = await _metadata.GetNodesAsync(cancellationToken);
        var targetNodes = _nodeSelectionStrategy.SelectNodes(nodes);
        var timestamps = _metadata.GetCausalTimestamps(cancellationToken);
        
        foreach (var node in targetNodes)
        {
            // _logger.LogDebug("Starting to sync metadata with node '{NodeId}'", node.Id.ToString());
            var start = DateTime.Now;
            
            var client = _clientPool.GetClient(node);
            using var duplexStream = client.GetMetadataDuplexStream();
            var otherTimestamps = await ExchangeMetadataTimestampsAsync(duplexStream, timestamps, cancellationToken);
            await ExchangeMetadataDeltasAsync(duplexStream, otherTimestamps, cancellationToken);
            
            _logger.LogDebug("Metadata sync with node '{NodeId}' completed in {Duration}", 
                node.Id, DateTime.Now - start);
        }
    }

    private async Task<ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>>> ExchangeMetadataTimestampsAsync(IDuplexMetadataDeltasStream duplexStream,
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps, CancellationToken cancellationToken)
    {
        var nodeStart = DateTime.Now;
        var otherTimestamps = await duplexStream.ExchangeMetadataTimestampsAsync(timestamps, cancellationToken);
        _logger.LogDebug("Metadata causal timestamps exchanged in {Duration}", DateTime.Now - nodeStart);
        return otherTimestamps;
    }

    private async Task ExchangeMetadataDeltasAsync(IDuplexMetadataDeltasStream duplexStream,
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> otherTimestamps, CancellationToken cancellationToken)
    {
        var task = ConsumeMetadataDeltasFromOtherNodeAsync(duplexStream, cancellationToken);
        foreach (var (kind, timestamp) in otherTimestamps)
        {
            var enumerable = _metadata.EnumerateDeltasAsync(kind, timestamp, cancellationToken);
            await duplexStream.SendDeltasAsync(kind, enumerable, cancellationToken);
        }

        await duplexStream.FinishSendingAsync();
        await task;
    }

    private async Task ConsumeDeltasFromOtherNodeAsync(IDuplexDeltasStream duplexStream,
        ManagedCrdt crdt,
        ShardId shardId, 
        CancellationToken cancellationToken)
    {
        var startReceivingBatches = DateTime.Now;
        var batchCount = 0;
        // receive deltas from another node
        await foreach (var deltas in duplexStream.GetDeltasAsync(cancellationToken))
        {
            ++batchCount;
            await crdt.MergeAsync(shardId, deltas, OperationContext.Default);
        }

        _logger.LogDebug("Received {Count} new delta batches, process took {Duration}", 
            batchCount, DateTime.Now - startReceivingBatches);
    }
    
    private async Task ConsumeMetadataDeltasFromOtherNodeAsync(IDuplexMetadataDeltasStream duplexStream, CancellationToken cancellationToken)
    {
        var startReceivingBatches = DateTime.Now;
        var batchCount = 0;
        // receive deltas from another node
        await foreach (var (kind, deltas) in duplexStream.GetDeltasAsync(cancellationToken))
        {
            ++batchCount;
            await _metadata.MergeAsync(kind, deltas, cancellationToken);
        }

        _logger.LogDebug("Received {Count} new metadata delta batches, process took {Duration}", 
            batchCount, DateTime.Now - startReceivingBatches);
    }
}