using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.Distribution;

public interface IDistributionStrategy
{
    ImmutableDictionary<ReplicaId, ImmutableArray<NodeInfo>> Distribute(in ImmutableArray<ReplicaInfo> orderedReplicaInfos, in ImmutableArray<NodeInfo> orderedNodes);
}