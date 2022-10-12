using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

internal sealed class PropagationService : IPropagationService
{
    private readonly IReplicaDistributor _distributor;
    private readonly INodeSelectionStrategy _selectionStrategy;
    private readonly INodeClientPool _clientPool;
    
    public PropagationService(IReplicaDistributor distributor,
        INodeSelectionStrategy selectionStrategy,
        INodeClientPool clientPool)
    {
        _distributor = distributor;
        _selectionStrategy = selectionStrategy;
        _clientPool = clientPool;
    }

    public async Task PropagateAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> data, OperationContext context)
    {
        var nodesThatShouldHaveReplica = _distributor.GetNodesWithWriteReplicas(instanceId, shardId);
        var targetNodes = _selectionStrategy.SelectNodes(nodesThatShouldHaveReplica);
        foreach (var nodeInfo in targetNodes)
        {
            // never propagate to a node, which is the origin of data being propagated
            if (nodeInfo.Id == context.Origin) continue;
            
            var nNodes = context.AwaitPropagationToNNodes;
            if (nNodes != 0)
            {
                var client = _clientPool.GetClient(nodeInfo);
                await client.MergeAsync(instanceId, shardId, data, context with {AwaitPropagationToNNodes = nNodes - 1});    
            }
            else
            {
                // buffer
            }
        }
    }
}