using System.Collections.Immutable;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface INodeSelectionStrategy
{
    ImmutableArray<NodeInfo> SelectNodes(ImmutableArray<NodeInfo> nodes);
}