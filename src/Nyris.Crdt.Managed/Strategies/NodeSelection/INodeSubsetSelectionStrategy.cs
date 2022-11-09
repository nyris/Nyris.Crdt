using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.NodeSelection;

public interface INodeSubsetSelectionStrategy
{
    ImmutableArray<NodeInfo> SelectNodes(in ImmutableArray<NodeInfo> nodes);
}