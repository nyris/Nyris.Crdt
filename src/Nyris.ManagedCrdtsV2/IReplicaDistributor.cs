using System.Collections.Immutable;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IReplicaDistributor
{
    ImmutableArray<NodeInfo> GetDesiredNodesWithReplicas(InstanceId instanceId, ShardId shardId);
}