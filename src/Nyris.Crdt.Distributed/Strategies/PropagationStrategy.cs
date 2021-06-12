using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies
{
    internal sealed class PropagationStrategy : IPropagationStrategy
    {
        /// <inheritdoc />
        public IEnumerable<NodeId> GetTargetNodes(IEnumerable<NodeInfo> nodes, NodeId thisNodeId)
        {
            var orderedList = nodes.Select(info => info.Id).OrderBy(id => id).ToList();
            var thisNodePosition = orderedList.BinarySearch(thisNodeId);
            return new[] {orderedList[thisNodePosition == orderedList.Count - 1 ? 0 : thisNodePosition + 1]};
        }
    }
}