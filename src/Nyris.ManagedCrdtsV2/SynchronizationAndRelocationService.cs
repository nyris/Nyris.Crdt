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
    private readonly NodeInfo _thisNode;

    public SynchronizationAndRelocationService(ILogger<SynchronizationAndRelocationService> logger,
        IClusterMetadataManager metadata,
        INodeClientPool clientPool,
        INodeSelectionStrategy nodeSelectionStrategy,
        IManagedCrdtProvider crdts,
        IReplicaDistributor replicaDistributor, 
        NodeInfo thisNode)
    {
        _logger = logger;
        _metadata = metadata;
        _clientPool = clientPool;
        _nodeSelectionStrategy = nodeSelectionStrategy;
        _crdts = crdts;
        _replicaDistributor = replicaDistributor;
        _thisNode = thisNode;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(DelayTime, stoppingToken);
        var i = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var traceId = $"{_thisNode.Id}-sync-{i}";
            var context = new OperationContext(_thisNode.Id, 0, traceId, stoppingToken);
            try
            {
                await SingleRunAsync(context);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "TraceId '{TraceId}': Sync could not finish due to an unhandled exception. Next try in {Delay}",
                    traceId, DelayTime);
            }
            
            await Task.Delay(DelayTime, stoppingToken);
            ++i;
        }
    }

    private async Task SingleRunAsync(OperationContext context)
    {
        var start = DateTime.Now;
        await SyncMetadataAsync(context);
        await SyncManagedCrdtsAsync(context);
        _logger.LogDebug("TraceId '{TraceId}': Sync run finished in {Duration}, next run in {Delay}", 
            context.TraceId, DateTime.Now - start, DelayTime);
        
        // TODO: (1) remove local shards that should not be here
        // TODO: (2) upsert local shards that should be here into read replicas
        // TODO: (3) add asymmetry to sync, so that nodes can ask not to send deltas if they should not have them 
        
        // TODO: allow callbacks for added/removed/updated elements in crdt set/map 
    }

    private async Task SyncManagedCrdtsAsync(OperationContext context)
    {
        foreach (var instanceId in _crdts.InstanceIds)
        {
            if (!_crdts.TryGet(instanceId, out var crdt)) continue;

            foreach (var shardId in crdt.Shards)
            {
                // _logger.LogDebug("TraceId '{TraceId}': Starting to sync crdt ({InstanceId}, {ShardId})",
                //     traceId, instanceId, shardId);
                var nodesWithReplica = _replicaDistributor.GetNodesWithWriteReplicas(instanceId, shardId);
                var nodesToSync = _nodeSelectionStrategy.SelectNodes(nodesWithReplica);
                var timestamp = crdt.GetCausalTimestamp(shardId);

                var dropAfterSync = !nodesWithReplica.Contains(_thisNode);

                // if (dropAfterSync)
                // {
                //     await crdt.WriteLock.WaitAsync(context.CancellationToken);
                //     // lock new writes, sync, drop shard
                // }
                
                foreach (var node in nodesToSync)
                {
                    // _logger.LogDebug("TraceId '{TraceId}': Starting to sync crdt ({InstanceId}, {ShardId}) with node '{NodeId}'", 
                        // traceId, instanceId, shardId, node.Id.ToString());
                    var start = DateTime.Now;
                    
                    var client = _clientPool.GetClient(node);
                    using var duplexStream = client.GetDeltaDuplexStream();
                    var otherTimestamp = await ExchangeCausalTimestampsAsync(duplexStream, instanceId, shardId, timestamp, context);
                    await ExchangeDeltasAsync(duplexStream, crdt, shardId, otherTimestamp, context);
                    
                    _logger.LogDebug("TraceId '{TraceId}': Syncing of crdt ({InstanceId}, {ShardId}) with node '{NodeId}' finished in {Duration}", 
                        context.TraceId, instanceId, shardId, node.Id, DateTime.Now - start);
                }
            }
        }
    }

    private async Task ExchangeDeltasAsync(IDuplexDeltasStream duplexStream,
        ManagedCrdt crdt,
        ShardId shardId,
        ReadOnlyMemory<byte> otherTimestamp,
        OperationContext context)
    {
        var task = ConsumeDeltasFromOtherNodeAsync(duplexStream, crdt, shardId, context);
        var enumerable = crdt.EnumerateDeltaBatchesAsync(shardId, otherTimestamp, context.CancellationToken);
        await duplexStream.SendDeltasAndFinishAsync(enumerable, context.CancellationToken);
        await task;
    }

    private async Task<ReadOnlyMemory<byte>> ExchangeCausalTimestampsAsync(IDuplexDeltasStream duplexStream,
        InstanceId instanceId,
        ShardId shardId,
        ReadOnlyMemory<byte> timestamp,
        OperationContext context)
    {
        var timestampsExchangeStart = DateTime.Now;
        var otherTimestamp = await duplexStream.ExchangeTimestampsAsync(instanceId, shardId, timestamp, context);
        _logger.LogDebug("TraceId '{TraceId}': Exchanged timestamps for ({InstanceId}, {ShardId}) in {Duration}",
            context.TraceId, instanceId, shardId, DateTime.Now - timestampsExchangeStart);
        return otherTimestamp;
    }

    private async Task SyncMetadataAsync(OperationContext context)
    {
        // _logger.LogDebug("Starting to sync node metadata");
        var nodes = await _metadata.GetNodesAsync(context.CancellationToken);
        var targetNodes = _nodeSelectionStrategy.SelectNodes(nodes);
        var timestamps = _metadata.GetCausalTimestamps(context.CancellationToken);
        
        foreach (var node in targetNodes)
        {
            // _logger.LogDebug("TraceId '{TraceId}': Starting to sync metadata with node '{NodeId}'", traceId, node.Id.ToString());
            var start = DateTime.Now;
            
            var client = _clientPool.GetClient(node);
            using var duplexStream = client.GetMetadataDuplexStream();
            var otherTimestamps = await ExchangeMetadataTimestampsAsync(duplexStream, timestamps, context);
            await ExchangeMetadataDeltasAsync(duplexStream, otherTimestamps, context);
            
            _logger.LogDebug("TraceId '{TraceId}': Metadata sync with node '{NodeId}' completed in {Duration}", 
                context.TraceId, node.Id, DateTime.Now - start);
        }
    }

    private async Task<ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>>> ExchangeMetadataTimestampsAsync(IDuplexMetadataDeltasStream duplexStream,
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps,
        OperationContext context)
    {
        var nodeStart = DateTime.Now;
        var otherTimestamps = await duplexStream.ExchangeMetadataTimestampsAsync(timestamps, context);
        _logger.LogDebug("TraceId '{TraceId}': Metadata causal timestamps exchanged in {Duration}", 
            context.TraceId, DateTime.Now - nodeStart);
        return otherTimestamps;
    }

    private async Task ExchangeMetadataDeltasAsync(IDuplexMetadataDeltasStream duplexStream,
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> otherTimestamps,
        OperationContext context)
    {
        var task = ConsumeMetadataDeltasFromOtherNodeAsync(duplexStream, context);
        foreach (var (kind, timestamp) in otherTimestamps)
        {
            var enumerable = _metadata.EnumerateDeltasAsync(kind, timestamp, context.CancellationToken);
            await duplexStream.SendDeltasAsync(kind, enumerable, context.CancellationToken);
        }

        await duplexStream.FinishSendingAsync();
        await task;
    }

    private async Task ConsumeDeltasFromOtherNodeAsync(IDuplexDeltasStream duplexStream,
        ManagedCrdt crdt,
        ShardId shardId, 
        OperationContext context)
    {
        var startReceivingBatches = DateTime.Now;
        var batchCount = 0;
        // receive deltas from another node
        await foreach (var deltas in duplexStream.GetDeltasAsync(context.CancellationToken))
        {
            ++batchCount;
            await crdt.MergeAsync(shardId, deltas, context);
        }

        _logger.LogDebug("TraceId '{TraceId}': Received {Count} new delta batches, process took {Duration}", 
            context.TraceId, batchCount, DateTime.Now - startReceivingBatches);
    }
    
    private async Task ConsumeMetadataDeltasFromOtherNodeAsync(IDuplexMetadataDeltasStream duplexStream,
        OperationContext context)
    {
        var startReceivingBatches = DateTime.Now;
        var batchCount = 0;
        // receive deltas from another node
        await foreach (var (kind, deltas) in duplexStream.GetDeltasAsync(context.CancellationToken))
        {
            ++batchCount;
            await _metadata.MergeAsync(kind, deltas, context);
        }

        _logger.LogDebug("TraceId: '{TraceId}': Received {Count} new metadata delta batches, process took {Duration}", 
            context.TraceId, batchCount, DateTime.Now - startReceivingBatches);
    }
}