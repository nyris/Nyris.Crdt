using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;
using Nyris.ManagedCrdtsV2.ManagedCrdts;
using Nyris.ManagedCrdtsV2.Strategies.NodeSelection;

namespace Nyris.ManagedCrdtsV2.Services.Hosted;

internal sealed class SynchronizationAndRelocationService : BackgroundService
{
    private static readonly TimeSpan DelayTime = TimeSpan.FromMinutes(1);

    private readonly Cluster _cluster;
    private readonly INodeClientFactory _clientFactory;
    private readonly INodeSelectionStrategy _nodeSelectionStrategy;
    private readonly NodeInfo _thisNode;
    private readonly ILogger<SynchronizationAndRelocationService> _logger;

    public SynchronizationAndRelocationService(Cluster cluster, 
        INodeClientFactory clientFactory,
        INodeSelectionStrategy nodeSelectionStrategy, 
        NodeInfo thisNode, 
        ILogger<SynchronizationAndRelocationService> logger)
    {
        _cluster = cluster;
        _clientFactory = clientFactory;
        _nodeSelectionStrategy = nodeSelectionStrategy;
        _thisNode = thisNode;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(DelayTime, stoppingToken);
        var i = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var traceId = $"{_thisNode.Id}-sync-#{i}";
            var context = new OperationContext(_thisNode.Id, -1, traceId, stoppingToken);
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
        
        // TODO: (3) add asymmetry to sync, so that nodes can ask not to send deltas if they should not have them 
        
        // TODO: allow callbacks for added/removed/updated elements in crdt set/map 
    }

    private async Task SyncManagedCrdtsAsync(OperationContext context)
    {
        foreach (var instanceId in _cluster.InstanceIds)
        {
            if (!_cluster.TryGet<ManagedCrdt>(instanceId, out var crdt)) continue;

            foreach (var shardId in crdt.Shards)
            {
                // _logger.LogDebug("TraceId '{TraceId}': Starting to sync crdt ({InstanceId}, {ShardId})",
                //     traceId, instanceId, shardId);
                var nodesWithReplica = _cluster.GetNodesWithWriteReplicas(instanceId, shardId);
                var shouldNotBeLocal = !nodesWithReplica.Contains(_thisNode);

                if (shouldNotBeLocal)
                { 
                    await RelocateAsync(crdt, instanceId, shardId, nodesWithReplica, context);
                }
                else
                {
                    await SyncShardAsync(crdt, shardId, nodesWithReplica, context, instanceId);
                }
                await _cluster.ReportSyncSuccessfulAsync(instanceId, shardId, context); 
            }
        }
    }

    
    private async Task RelocateAsync(ManagedCrdt crdt, InstanceId instanceId, ShardId shardId,
        ImmutableArray<NodeInfo> nodesWithReplica, OperationContext context)
    {
        await crdt.WriteLock.WaitAsync(context.CancellationToken);
        try
        {
            await SyncShardAsync(crdt, shardId, nodesWithReplica, context, instanceId);
            await crdt.DropShardAsync(shardId);  // shard is dropped here and not in ClusterManager cause of locking requirement
        }
        finally
        {
            crdt.WriteLock.Release();
        }
    }

    private async Task SyncShardAsync(ManagedCrdt crdt,
        ShardId shardId,
        ImmutableArray<NodeInfo> nodesWithReplica,
        OperationContext context,
        InstanceId instanceId)
    {
        var nodesToSync = _nodeSelectionStrategy.SelectNodes(nodesWithReplica);
        var timestamp = crdt.GetCausalTimestamp(shardId);
        
        foreach (var node in nodesToSync)
        {
            // _logger.LogDebug("TraceId '{TraceId}': Starting to sync crdt ({InstanceId}, {ShardId}) with node '{NodeId}'", 
            // traceId, instanceId, shardId, node.Id.ToString());
            var start = DateTime.Now;

            var client = _clientFactory.GetClient(node);
            using var duplexStream = client.GetDeltaDuplexStream();

            var otherTimestamp = await ExchangeCausalTimestampsAsync(duplexStream, instanceId, shardId, timestamp, context);
            await ExchangeDeltasAsync(duplexStream, crdt, shardId, otherTimestamp, context);

            _logger.LogDebug(
                "TraceId '{TraceId}': Syncing of crdt ({InstanceId}, {ShardId}) with node '{NodeId}' finished in {Duration}",
                context.TraceId, instanceId, shardId, node.Id, DateTime.Now - start);
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
        var nodes = await _cluster.GetNodesAsync(context.CancellationToken);
        var targetNodes = _nodeSelectionStrategy.SelectNodes(nodes);
        var timestamps = _cluster.GetCausalTimestamps(context.CancellationToken);
        
        foreach (var node in targetNodes)
        {
            // _logger.LogDebug("TraceId '{TraceId}': Starting to sync metadata with node '{NodeId}'", traceId, node.Id.ToString());
            var start = DateTime.Now;
            
            var client = _clientFactory.GetClient(node);
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
            var enumerable = _cluster.EnumerateDeltasAsync(kind, timestamp, context.CancellationToken);
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
            await _cluster.MergeAsync(kind, deltas, context);
        }

        _logger.LogDebug("TraceId: '{TraceId}': Received {Count} new metadata delta batches, process took {Duration}", 
            context.TraceId, batchCount, DateTime.Now - startReceivingBatches);
    }
}