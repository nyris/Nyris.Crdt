using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Strategies.NodeSelection;

public interface INodeSelectionStrategy
{
    ImmutableArray<NodeInfo> SelectNodes(ImmutableArray<NodeInfo> nodes);
}