using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Model.Deltas;

namespace Nyris.Crdt.Managed.Metadata;

internal sealed class CrdtInfos : ObservedRemoveMapV2<NodeId, ReplicaId, CrdtInfo, CrdtInfoDelta, CrdtInfoCausalTimestamp>
{
    public bool TryUpsertNodeAsHolderOfReadReplica(NodeId thisNode, NodeId nodeId, in ReplicaId replica, out ImmutableArray<DeltaDto> deltas)
    {
        if (!TryGet(replica, out var info))
        {
            deltas = ImmutableArray<DeltaDto>.Empty;
            return false;
        }

        if (info.DoesNodeHaveReadReplica(nodeId))
        {
            deltas = ImmutableArray<DeltaDto>.Empty;
            return true;  // while we didn't add anything, upsert is still successful
        }
        
        return TryMutate(thisNode, replica, crdtInfo => crdtInfo.AddNodeAsHoldingReadReplica(nodeId, thisNode), out deltas);
    }
    
    public bool TryRemoveNodeAsHolderOfReadReplica(NodeId thisNode, NodeId nodeId, in ReplicaId replica, out ImmutableArray<DeltaDto> deltas)
    {
        if (!TryGet(replica, out var info))
        {
            deltas = ImmutableArray<DeltaDto>.Empty;
            return false;
        }

        if (!info.DoesNodeHaveReadReplica(nodeId))
        {
            deltas = ImmutableArray<DeltaDto>.Empty;
            return true;  // while we didn't add anything, remove is still successful
        }
        
        return TryMutate(thisNode, replica, crdtInfo => crdtInfo.RemoveNodeFromReadReplicas(nodeId), out deltas);
    }
}