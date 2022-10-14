using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.NodeSelection;

public interface INodeSelectionStrategy
{
    ImmutableArray<NodeInfo> SelectNodes(ImmutableArray<NodeInfo> nodes);
}