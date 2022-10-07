using System.Collections.Immutable;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IDistributionStrategy
{
    ImmutableDictionary<GlobalShardId, ImmutableArray<NodeInfo>> Distribute(ImmutableArray<ShardInfo> shardInfos, ImmutableArray<NodeInfo> nodes);
}