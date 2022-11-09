using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.ManagedCrdts;

public abstract class ManagedCrdt<TCrdt, TDelta, TTimeStamp> : ManagedCrdt
    where TCrdt : IDeltaCrdt<TDelta, TTimeStamp>, new()
{
    private readonly Dictionary<ShardId, (bool IsReadReplica, TCrdt Shard)> _shards = new();
    private readonly ReaderWriterLockSlim _shardsLock = new();
    
    private readonly ISerializer _serializer;
    private readonly IPropagationService _propagationService;

    internal sealed override ICollection<ShardId> ShardIds
    {
        get
        {
            _shardsLock.EnterReadLock();
            try
            {
                var ids = new ShardId[_shards.Count];
                _shards.Keys.CopyTo(ids, 0);
                return ids;
            }
            finally
            {
                _shardsLock.ExitReadLock();
            }
        }
    }

    protected ManagedCrdt(InstanceId instanceId,
        ISerializer serializer,
        IPropagationService propagationService) : base(instanceId)
    {
        _serializer = serializer;
        _propagationService = propagationService;
    }

    public sealed override async Task MergeAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context)
    {
        var deltas = _serializer.Deserialize<ImmutableArray<TDelta>>(batch);
        var (_, shard) = GetOrCreateShard(shardId);
        var result = shard.Merge(deltas);
        if (result == DeltaMergeResult.StateUpdated)
        {
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

    public sealed override ReadOnlyMemory<byte> GetCausalTimestamp(in ShardId shardId) =>
        !TryGetShard(shardId, out var crdt, out _) 
            ? ReadOnlyMemory<byte>.Empty 
            : _serializer.Serialize(crdt.GetLastKnownTimestamp());
    
    internal sealed override void MarkLocalShardAsReadReplica(in ShardId shardId)
    {
        _shardsLock.EnterWriteLock();
        try
        {
            if (_shards.TryGetValue(shardId, out var tuple))
            {
                _shards[shardId] = (true, tuple.Shard);
            }
            else
            {
                _shards[shardId] = (true, CreateShard(shardId));
            }
        }
        finally
        {
            _shardsLock.ExitWriteLock();
        }
    }

    internal sealed override Dictionary<ShardId, int> GetShardSizes()
    {
        // TODO: this was used for debugging, remove 
        var result = new Dictionary<ShardId, int>();

        _shardsLock.EnterReadLock();
        try
        {
            foreach (var (shardId, (_, shard)) in _shards)
            {
                if (shard is OptimizedObservedRemoveSetV2<NodeId, int> set)
                {
                    result[shardId] = set.Values.Count;
                }

                if (shard is ObservedRemoveSetV3<NodeId, double> setV3)
                {
                    result[shardId] = setV3.Count;
                }
            }
        }
        finally
        {
            _shardsLock.ExitReadLock();
        }

        return result;
    }
    
    protected internal override Task DropShardAsync(in ShardId shardId)
    {
        _shardsLock.EnterWriteLock();
        try
        {
            _shards.Remove(shardId);
        }
        finally
        {
            _shardsLock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    protected Task PropagateAsync(in ShardId shardId, in ImmutableArray<TDelta> deltas, OperationContext context) 
        => _propagationService.PropagateAsync(InstanceId, shardId, _serializer.Serialize(deltas), context);

    protected (bool IsReadReplica, TCrdt Shard) GetOrCreateShard(in ShardId shardId)
    {
        _shardsLock.EnterWriteLock();
        try
        {
            if (_shards.TryGetValue(shardId, out var result))
            {
                return result;
            }

            return _shards[shardId] = (false, CreateShard(shardId));
        }
        finally
        {
            _shardsLock.ExitWriteLock();
        }
    }

    protected virtual TCrdt CreateShard(in ShardId shardId) => new();

    protected bool TryGetShard(in ShardId shardId, [NotNullWhen(true)] out TCrdt? crdt, out bool isReadReplica)
    {
        _shardsLock.EnterReadLock();
        try
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
        finally
        {
            _shardsLock.ExitReadLock();
        }
    }
}