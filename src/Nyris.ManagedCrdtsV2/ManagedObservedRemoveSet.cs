using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2;

public class ObservedRemoveSetTimestamp {} // TODO: make a full timestamp, not just version-vector

public class ManagedObservedRemoveSet<TItem> : ManagedCrdt<OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto, ObservedRemoveSetTimestamp>
    where TItem : IEquatable<TItem>, IComparable<TItem>
{
    private readonly OptimizedObservedRemoveSetV2<NodeId, TItem> _set = new();
    
    private readonly NodeId _thisNode;
    private readonly IPropagationStrategy<OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto> _propagationStrategy;
    private readonly ClusterRouter _clusterRouter;

    public ManagedObservedRemoveSet(InstanceId instanceId,
        NodeId thisNode,
        IPropagationStrategy<OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto> propagationStrategy,
        ClusterRouter clusterRouter,
        ISerializer serializer) : base(instanceId, serializer)
    {
        _thisNode = thisNode;
        _propagationStrategy = propagationStrategy;
        _clusterRouter = clusterRouter;
    }

    public async Task AddAsync(TItem item, OperationContext context)
    {
        if (!_clusterRouter.IsLocal(InstanceId, out var scope))
        {
            // _clusterRouter.Reroute()
        }
        
        using (scope) 
        {
            var dtos = _set.Add(item, _thisNode);
            await _propagationStrategy.PropagateAsync(dtos);
        }
    }

    public async Task RemoveAsync(TItem item, OperationContext context)
    {
        if (!_clusterRouter.IsLocal(InstanceId, out var scope))
        {
            // _clusterRouter.Reroute()
        }
        
        using (scope)
        {
            var dtos = _set.Remove(item);
            await _propagationStrategy.PropagateAsync(dtos);
        }
    }

    public override Task MergeDeltaBatchAsync(OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto[] deltas, OperationContext context)
    {
        foreach (var delta in deltas)
        {
            _set.Merge(delta);
        }
        return Task.CompletedTask;
    }

    public override async IAsyncEnumerable<OptimizedObservedRemoveSetV2<NodeId, TItem>.DeltaDto[]> EnumerateDeltaBatchesAsync(ObservedRemoveSetTimestamp since)
    {
        // TODO: pass 'since' once TTimeStamp is fixed
        foreach (var dto in _set.EnumerateDeltaDtos().Chunk(50))
        {
            yield return dto;
        }
    }

    public override ReadOnlyMemory<byte> GetHash()
    {
        throw new NotImplementedException();
    }
}