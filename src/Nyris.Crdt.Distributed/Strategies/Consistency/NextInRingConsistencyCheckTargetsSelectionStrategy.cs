using System;
using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies.Consistency
{
    internal sealed class NextInRingConsistencyCheckTargetsSelectionStrategy : IConsistencyCheckTargetsSelectionStrategy
    {
        /// <inheritdoc />
        public IEnumerable<NodeId> GetTargetNodes(IEnumerable<NodeInfo> nodes, NodeId thisNodeId)
        {
            var orderedList = nodes.Select(info => info.Id).OrderBy(id => id).ToList();
            if(orderedList.Count <= 1) return ArraySegment<NodeId>.Empty;

            try
            {
                var thisNodePosition = orderedList.BinarySearch(thisNodeId);
                return new[] { orderedList[thisNodePosition == orderedList.Count - 1 ? 0 : thisNodePosition + 1] };
            }
            catch (ArgumentOutOfRangeException e)
            {
                return new[] { orderedList.First() };
            }
        }
    }
}