
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2;

public class ManagedObservedRemoveSet<TItem> 
    : ManagedCrdt<
        OptimizedObservedRemoveSetV2<NodeId, TItem>,
        OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto, 
        OptimizedObservedRemoveSetV2<NodeId, TItem>.CausalTimestamp>
    where TItem : IEquatable<TItem>, IComparable<TItem>
{
    private readonly NodeId _thisNode;

    public ManagedObservedRemoveSet(InstanceId instanceId,
        NodeId thisNode, 
        ISerializer serializer, 
        IPropagationService propagationService,
        ILogger logger) 
        : base(instanceId, serializer, propagationService, logger)
    {
        _thisNode = thisNode;
    }

    public async Task AddAsync(TItem item, OperationContext context)
    {
        Logger.LogDebug("Adding {Item} to set", item.ToString());
        var shard = GetOrCreateShard(DefaultShard);
        var deltas = shard.Add(item, _thisNode);
        await PropagateAsync(DefaultShard, deltas, context.CancellationToken);
        Logger.LogDebug("Addition of {Item} finished", item.ToString());
    }

    public async Task RemoveAsync(TItem item, OperationContext context)
    {
        
    }

    public override ReadOnlyMemory<byte> GetHash(ShardId shardId)
    {
        throw new NotImplementedException();
    }
}