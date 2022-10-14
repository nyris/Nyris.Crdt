using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Strategies.Distribution;

public interface IDistributionStrategy
{
    ImmutableDictionary<ReplicaId, ImmutableArray<NodeInfo>> Distribute(in ImmutableArray<ReplicaInfo> orderedShardInfos, in ImmutableArray<NodeInfo> orderedNodes);
}