using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.NodeSelection;

public interface INodeSelectionStrategy
{
    NodeInfo SelectNode(in ImmutableArray<NodeInfo> nodes);
}