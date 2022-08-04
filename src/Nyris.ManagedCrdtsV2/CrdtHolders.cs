using System.Collections.Concurrent;
using System.Diagnostics;
using Nyris.Crdt;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2;

public sealed class CrdtHolders
{
    private readonly ConcurrentDictionary<InstanceId, ManagedCrdt> _crdts = new();

    // Add/Remove
}


public sealed class CrdtInfo : IDeltaCrdt<CrdtInfo.DeltaDto, CrdtInfo.CausalTimestamp>
{

    public CausalTimestamp GetLastKnownTimestamp()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? since = default)
    {
        throw new NotImplementedException();
    }

    public void Merge(DeltaDto delta)
    {
        throw new NotImplementedException();
    }


    public class DeltaDto {}
    public class CausalTimestamp {}
}

public sealed class ClusterRouter
{
    private readonly ReaderWriterLockSlim _redistributionLock = new();
    private readonly OptimizedObservedRemoveSetV2<NodeId, NodeId> _nodeSet = new();

    public ClusterRouter(NodeId thisNodeId)
    {
        _nodeSet.Add(thisNodeId, thisNodeId);
    }

    /// <summary>
    /// Checks if this CRDT is local and thus can accept operations or if operation should be rerouted
    /// </summary>
    /// <remarks>If method returns true, OperationScope MUST be disposed at some point,
    /// otherwise redistribution can not happen</remarks>
    /// <param name="instanceId"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    public bool IsLocal(InstanceId instanceId, out OperationScope scope)
    {
        // if not local
        scope = default;
        return false;
        
        _redistributionLock.EnterReadLock();
        scope = new OperationScope(_redistributionLock);
        return true;
    }
}




public interface IPropagationStrategy<in TDelta> {
    Task PropagateAsync(IReadOnlyList<TDelta> dto, CancellationToken cancellationToken = default);
    void MaybePropagateLater(TDelta dto);
}
