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

    public async Task PropagateAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var nodesThatShouldHaveReplica = _distributor.GetDesiredNodesWithReplicas(instanceId, shardId);
        var targetNodes = _selectionStrategy.SelectNodes(nodesThatShouldHaveReplica);
        foreach (var nodeInfo in targetNodes)
        {
            var client = _clientPool.GetClient(nodeInfo);
            await client.MergeAsync(instanceId, shardId, data, cancellationToken);
        }
        
        // buffering idea:
        // 1. add deltas to buffer: object[]
    }
}