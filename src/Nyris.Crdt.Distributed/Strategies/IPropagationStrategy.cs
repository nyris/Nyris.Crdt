using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies
{
    public interface IPropagationStrategy
    {
        IEnumerable<NodeId> GetTargetNodes(IEnumerable<NodeInfo> nodes, NodeId thisNodeId);
    }
}