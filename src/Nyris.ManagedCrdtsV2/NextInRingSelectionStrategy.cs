using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

internal sealed class NextInRingSelectionStrategy : INodeSelectionStrategy
{
    private readonly NodeInfo _thisNode;
    private static readonly IReadOnlyCollection<NodeInfo> Empty = ArraySegment<NodeInfo>.Empty;

    public NextInRingSelectionStrategy(NodeInfo thisNode)
    {
        _thisNode = thisNode;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<NodeInfo> SelectNodes(IReadOnlyCollection<NodeInfo> nodes)
    {
        var orderedList = nodes.OrderBy(info => info.Id).ToList();
        if (orderedList.Count <= 1) return Empty;

        try
        {
            var thisNodePosition = orderedList.BinarySearch(_thisNode);
            return new[] { orderedList[thisNodePosition == orderedList.Count - 1 ? 0 : thisNodePosition + 1] };
        }
        catch (ArgumentOutOfRangeException)
        {
            return new[] { orderedList.First() };
        }
    }
}