using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Strategies.NodeSelection;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.Services;

internal sealed class ReroutingService : IReroutingService
{
    private readonly IReplicaDistributor _distributor;
    private readonly INodeClientFactory _clientFactory;
    private readonly INodeSelectionStrategy _selectionStrategy;
    private readonly NodeInfo _thisNode;

    public ReroutingService(IReplicaDistributor distributor, INodeClientFactory clientFactory, INodeSelectionStrategy selectionStrategy, NodeInfo thisNode)
    {
        _distributor = distributor;
        _clientFactory = clientFactory;
        _selectionStrategy = selectionStrategy;
        _thisNode = thisNode;
    }

    public async Task<ReadOnlyMemory<byte>> RerouteAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> operation, OperationContext context)
    {
        var nodesThatShouldHaveReplica = _distributor.GetNodesWithReadReplicas(instanceId, shardId);
        var targetNode = _selectionStrategy.SelectNode(nodesThatShouldHaveReplica);

        if (targetNode == _thisNode)
        {
            throw new RoutingException($"TraceId '{context.TraceId}': can not reroute to myself");
        }

        if (targetNode.Id == context.Origin)
        {
            throw new RoutingException($"TraceId '{context.TraceId}': cycle detected during rerouting, " +
                                       $"must not reroute to origin {context.Origin.ToString()}");
        }
        
        var client = _clientFactory.GetClient(targetNode);
        return await client.RerouteAsync(instanceId, shardId, operation, context);
    }
}