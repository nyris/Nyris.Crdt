using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.ManagedCrdtsV2;

public abstract class ManagedCrdt<TCrdt, TDelta, TTimeStamp> 
    : ManagedCrdt
    where TCrdt : IDeltaCrdt<TDelta, TTimeStamp>, new()
{
    private readonly ConcurrentDictionary<ShardId, TCrdt> _shards = new();
    private readonly ISerializer _serializer;
    private readonly IPropagationService _propagationService;
    
    protected readonly ILogger Logger;

    // ReSharper disable once StaticMemberInGenericType
    protected static readonly ShardId DefaultShard = default;
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

    public sealed override async Task MergeAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context)
    {
        var deltas = _serializer.Deserialize<ImmutableArray<TDelta>>(batch);
        
        // Logger.LogDebug("Merging delta batch for shardId {ShardId}: {Deltas}", 
        //     shardId.AsUint, JsonConvert.SerializeObject(deltas));
        var shard = GetOrCreateShard(shardId);
        var result = shard.Merge(deltas);
        if (result == DeltaMergeResult.StateUpdated)
        {
            // Logger.LogDebug("Deltas were new to this replica, propagating further");
            await _propagationService.PropagateAsync(InstanceId, shardId, batch, context.CancellationToken);
        }
    }

    public sealed override async IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ShardId shardId,
        ReadOnlyMemory<byte> causalTimestampBin,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if(!_shards.TryGetValue(shardId, out var crdt)) yield break;
        
        var causalTimestamp = causalTimestampBin.IsEmpty 
            ? default 
            : _serializer.Deserialize<TTimeStamp>(causalTimestampBin);
        
        foreach (var batch in crdt.EnumerateDeltaDtos(causalTimestamp).Chunk(200))
        {
            if(cancellationToken.IsCancellationRequested) yield break;
            yield return _serializer.Serialize(batch);
        }
    }

    public override ReadOnlyMemory<byte> GetCausalTimestamp(ShardId shardId) =>
        !_shards.TryGetValue(shardId, out var crdt) 
            ? ReadOnlyMemory<byte>.Empty 
            : _serializer.Serialize(crdt.GetLastKnownTimestamp());

    protected Task PropagateAsync(ShardId shardId, ImmutableArray<TDelta> deltas, CancellationToken cancellationToken)
    {
        // buffering can done here..
        return _propagationService.PropagateAsync(InstanceId, shardId, _serializer.Serialize(deltas), cancellationToken);
    }

    protected TCrdt GetOrCreateShard(ShardId shardId) => _shards.GetOrAdd(shardId, _ => new TCrdt());
}