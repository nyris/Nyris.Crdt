using Nyris.Crdt.Distributed.Model;
using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Strategies.Propagation
{
    public interface IPropagationStrategy
    {
        IEnumerable<NodeId> GetTargetNodes(IEnumerable<NodeInfo> nodes, NodeId thisNodeId);
    }
}
