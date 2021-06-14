using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies.Consistency
{
    public interface IConsistencyCheckTargetsSelectionStrategy
    {
        IEnumerable<NodeId> GetTargetNodes(IEnumerable<NodeInfo> nodes, NodeId thisNodeId);
    }
}