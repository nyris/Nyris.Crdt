using System.Collections.Immutable;
using System.Diagnostics;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.NodeSelection;

internal sealed class NextInRingSelectionStrategy : INodeSubsetSelectionStrategy, INodeSelectionStrategy
{
    private readonly NodeInfo _thisNode;

    public NextInRingSelectionStrategy(NodeInfo thisNode)
    {
        _thisNode = thisNode;
    }

    /// <inheritdoc />
    public ImmutableArray<NodeInfo> SelectNodes(in ImmutableArray<NodeInfo> nodes)
    {
        return nodes.Length == 0 ? ImmutableArray<NodeInfo>.Empty : ImmutableArray.Create(SelectNode(nodes));
    }

    public NodeInfo SelectNode(in ImmutableArray<NodeInfo> nodes)
    {
        Debug.Assert(nodes.Length > 0);
        var orderedList = nodes.OrderBy(info => info.Id).ToList();
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