using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.NodeSelection;

internal sealed class NextInRingSelectionStrategy : INodeSelectionStrategy
{
    private readonly NodeInfo _thisNode;
    private static readonly ImmutableArray<NodeInfo> Empty = ImmutableArray<NodeInfo>.Empty;

    public NextInRingSelectionStrategy(NodeInfo thisNode)
    {
        _thisNode = thisNode;
    }

    /// <inheritdoc />
    public ImmutableArray<NodeInfo> SelectNodes(ImmutableArray<NodeInfo> nodes)
    {
        var orderedList = nodes.OrderBy(info => info.Id).ToList();
        if (orderedList.Count <= 1) return Empty;

        try
        {
            var thisNodePosition = orderedList.BinarySearch(_thisNode);
            return ImmutableArray.Create(orderedList[thisNodePosition == orderedList.Count - 1 ? 0 : thisNodePosition + 1]);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ImmutableArray.Create(orderedList.First());
        }
    }
}