using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Strategies.NodeSelection;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.Services.Propagation;

internal sealed class PropagationService : IPropagationService
{
    private readonly IReplicaDistributor _distributor;
    private readonly INodesSelectionStrategy _selectionStrategy;
    private readonly INodeClientFactory _clientFactory;
    
    public PropagationService(IReplicaDistributor distributor,
        INodesSelectionStrategy selectionStrategy,
        INodeClientFactory clientFactory)
    {
        _distributor = distributor;
        _selectionStrategy = selectionStrategy;
        _clientFactory = clientFactory;
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
                var client = _clientFactory.GetClient(nodeInfo);
                await client.MergeAsync(instanceId, shardId, data, context with {AwaitPropagationToNNodes = nNodes - 1});    
            }
            else
            {
                // TODO: buffer data here
            }
        }
    }
}