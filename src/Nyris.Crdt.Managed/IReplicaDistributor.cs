using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed;

internal interface IReplicaDistributor
{
    ImmutableArray<NodeInfo> GetNodesWithWriteReplicas(InstanceId instanceId, ShardId shardId);
    ImmutableArray<NodeInfo> GetNodesWithReadReplicas(InstanceId instanceId, ShardId shardId);
}