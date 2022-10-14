using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.NodeSelection;

internal sealed class NextInRingSelectionStrategy : INodesSelectionStrategy, INodeSelectionStrategy
{
    private readonly NodeInfo _thisNode;

    public NextInRingSelectionStrategy(NodeInfo thisNode)
    {
        _thisNode = thisNode;
    }

    /// <inheritdoc />
    public ImmutableArray<NodeInfo> SelectNodes(in ImmutableArray<NodeInfo> nodes)
    {
        return nodes.Length <= 1 ? ImmutableArray<NodeInfo>.Empty : ImmutableArray.Create(SelectNode(nodes));
    }

    public NodeInfo SelectNode(in ImmutableArray<NodeInfo> nodes)
    {
        var orderedList = nodes.OrderBy(info => info.Id).ToList();
        if (orderedList.Count <= 1) return _thisNode;

        try
        {
            var thisNodePosition = orderedList.BinarySearch(_thisNode);
            return orderedList[thisNodePosition == orderedList.Count - 1 ? 0 : thisNodePosition + 1];
        }
        catch (ArgumentOutOfRangeException)
        {
            return orderedList.First();
        }
    }
}