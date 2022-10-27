using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed;

internal sealed partial class Cluster : IReplicaDistributor
{
    public ImmutableArray<NodeInfo> GetNodesWithWriteReplicas(InstanceId instanceId, ShardId shardId) 
        => _desiredDistribution.TryGetValue(instanceId.With(shardId), out var nodes) 
            ? nodes 
            : ImmutableArray<NodeInfo>.Empty;

    public ImmutableArray<NodeInfo> GetNodesWithReadReplicas(InstanceId instanceId, ShardId shardId)
    {
        if (!_crdtInfos.TryGet(instanceId.With(shardId), crdtInfo => crdtInfo.ReadReplicas, out var replicas)
            || replicas is null 
            || replicas.Count == 0)
        {
            return ImmutableArray<NodeInfo>.Empty;
        }

        // TODO: refactor this abomination - cache read replica upon change in CrdtInfos
        var builder = ImmutableArray.CreateBuilder<NodeInfo>(replicas.Count);
        var nodes = _nodeSet.Values.ToDictionary(ni => ni.Id, ni => ni);
        foreach (var nodeId in replicas.OrderBy(i => i))
        {
            if (nodes.TryGetValue(nodeId, out var info))
            {
                builder.Add(info);    
            }
        }
        return builder.MoveToImmutable();
    }
}