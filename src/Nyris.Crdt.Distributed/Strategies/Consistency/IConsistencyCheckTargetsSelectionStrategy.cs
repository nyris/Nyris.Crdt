using Nyris.Crdt.Distributed.Model;
using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Strategies.Consistency;

public interface IConsistencyCheckTargetsSelectionStrategy
{
    IEnumerable<NodeId> GetTargetNodes(IEnumerable<NodeInfo> nodes, NodeId thisNodeId);
}
