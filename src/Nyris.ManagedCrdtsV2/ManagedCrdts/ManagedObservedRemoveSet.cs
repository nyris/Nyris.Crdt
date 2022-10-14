using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;
using Nyris.ManagedCrdtsV2.Services;

namespace Nyris.ManagedCrdtsV2.ManagedCrdts;

public class ManagedObservedRemoveSet<TItem> 
    : ManagedCrdt<
        OptimizedObservedRemoveSetV2<NodeId, TItem>,
        OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto, 
        OptimizedObservedRemoveSetV2<NodeId, TItem>.CausalTimestamp>
    where TItem : IEquatable<TItem>, IComparable<TItem>
{
    private readonly NodeId _thisNode;
    private ulong _counter;
    
    public ManagedObservedRemoveSet(InstanceId instanceId,
        NodeId thisNode, 
        ISerializer serializer, 
        IPropagationService propagationService,
        ILogger logger) 
        : base(instanceId, serializer, propagationService, logger)
    {
        _thisNode = thisNode;
    }

    public async Task AddAsync(TItem item, CancellationToken cancellationToken)
    {
        // Logger.LogDebug("Adding {Item} to set", item.ToString());
        var shardId = ShardId.FromUint((uint)Math.Abs(item.GetHashCode()) % 3);
        ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto> deltas;
        
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            // for addition we can always accept write locally, it will be relocated later if necessary 
            var (_, shard) = GetOrCreateShard(shardId);
            deltas = shard.Add(item, _thisNode);
        }
        finally
        {
            WriteLock.Release();
        }

        var context = new OperationContext(_thisNode, 1, GetTraceId("add"), cancellationToken);
        await PropagateAsync(shardId, deltas, context);
        // Logger.LogDebug("Addition of {Item} finished", item.ToString());
    }

    public async Task RemoveAsync(TItem item, CancellationToken cancellationToken)
    {
        var shardId = ShardId.FromUint((uint)Math.Abs(item.GetHashCode()) % 3);
        var deltas = ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto>.Empty;
        
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var (isReadReplica, shard) = GetOrCreateShard(shardId);

            if (isReadReplica)
            {
                deltas = shard.Remove(item);
            }
            else
            {
                // package into operation 
                // call Reroute
            }
        }
        finally
        {
            WriteLock.Release();
        }

        if (!deltas.IsDefaultOrEmpty)
        {
            var context = new OperationContext(_thisNode, 1, GetTraceId("del"), cancellationToken);
            await PropagateAsync(shardId, deltas, context);    
        }
    }

    private string GetTraceId(string method) => $"{InstanceId}-{method}-#{Interlocked.Increment(ref _counter)}";
}