using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface INodeSelectionStrategy
{
    IReadOnlyCollection<NodeInfo> SelectNodes(IReadOnlyCollection<NodeInfo> nodes);
}