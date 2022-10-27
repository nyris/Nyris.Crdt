using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.ManagedCrdts;

public abstract class ManagedCrdt<TCrdt, TDelta, TTimeStamp> : ManagedCrdt
    where TCrdt : IDeltaCrdt<TDelta, TTimeStamp>, new()
{
    private readonly ConcurrentDictionary<ShardId, (bool IsReadReplica, TCrdt Shard)> _shards = new();
    private readonly ISerializer _serializer;
    private readonly IPropagationService _propagationService;
    
    protected readonly ILogger Logger;

    internal sealed override ICollection<ShardId> Shards => _shards.Keys;

    protected ManagedCrdt(InstanceId instanceId,
        ISerializer serializer,
        IPropagationService propagationService,
        ILogger logger) : base(instanceId)
    {
        _serializer = serializer;
        _propagationService = propagationService;
        Logger = logger;
    }

    internal sealed override void MarkLocalShardAsReadReplica(ShardId shardId)
    {
        _shards.AddOrUpdate(shardId, 
            _ => new ValueTuple<bool, TCrdt>(true, new TCrdt()),
            (_, tuple) => new ValueTuple<bool, TCrdt>(true, tuple.Shard));
    }

    internal sealed override Dictionary<ShardId, int> GetShardSizes()
    {
        // TODO: this was used for debugging, remove 

        var result = new Dictionary<ShardId, int>();

        foreach (var (shardId, (_, shard)) in _shards)
        {
            if (shard is OptimizedObservedRemoveSetV2<NodeId, int> set)
            {
                result[shardId] = set.Values.Count;
            }

            if (shard is OptimizedObservedRemoveSetV3<NodeId, double> setV3)
            {
                result[shardId] = setV3.Count;
            }
        }

        return result;
    }

    public sealed override async Task MergeAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context)
    {
        var deltas = _serializer.Deserialize<ImmutableArray<TDelta>>(batch);
        
        // Logger.LogDebug("Merging delta batch for shardId {ShardId}: {Deltas}", 
        //     shardId.AsUint, JsonConvert.SerializeObject(deltas));
        var (_, shard) = GetOrCreateShard(shardId);
        var result = shard.Merge(deltas);
        if (result == DeltaMergeResult.StateUpdated)
        {
            // Logger.LogDebug("Deltas were new to this replica, propagating further");
            await _propagationService.PropagateAsync(InstanceId, shardId, batch, context);
        }
    }

    public sealed override async IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ShardId shardId,
        ReadOnlyMemory<byte> causalTimestampBin,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if(!TryGetShard(shardId, out var crdt, out _)) yield break;
        
        var causalTimestamp = causalTimestampBin.IsEmpty 
            ? default 
            : _serializer.Deserialize<TTimeStamp>(causalTimestampBin);
        
        foreach (var batch in crdt.EnumerateDeltaDtos(causalTimestamp).Chunk(200))
        {
            if(cancellationToken.IsCancellationRequested) yield break;
            yield return _serializer.Serialize(batch);
        }
    }

    public sealed override ReadOnlyMemory<byte> GetCausalTimestamp(ShardId shardId) =>
        !TryGetShard(shardId, out var crdt, out _) 
            ? ReadOnlyMemory<byte>.Empty 
            : _serializer.Serialize(crdt.GetLastKnownTimestamp());

    internal sealed override Task DropShardAsync(ShardId shardId)
    {
        _shards.TryRemove(shardId, out _);
        return Task.CompletedTask;
    }

    protected Task PropagateAsync(in ShardId shardId, in ImmutableArray<TDelta> deltas, OperationContext context) 
        => _propagationService.PropagateAsync(InstanceId, shardId, _serializer.Serialize(deltas), context);

    protected bool TryGetShard(in ShardId shardId, [NotNullWhen(true)] out TCrdt? crdt, out bool isReadReplica)
    {
        if (_shards.TryGetValue(shardId, out var tuple))
        {
            crdt = tuple.Shard;
            isReadReplica = tuple.IsReadReplica;
            return true;
        }

        crdt = default;
        isReadReplica = false;
        return false;
    } 
    
    protected (bool IsReadReplica, TCrdt Shard) GetOrCreateShard(in ShardId shardId) 
        => _shards.GetOrAdd(shardId, _ => new ValueTuple<bool, TCrdt>(false, new TCrdt()));
}