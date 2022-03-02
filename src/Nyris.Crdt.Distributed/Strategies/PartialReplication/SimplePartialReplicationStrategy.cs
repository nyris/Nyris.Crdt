using System;
using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies.PartialReplication
{
    public sealed class SimplePartialReplicationStrategy : IPartialReplicationStrategy
    {
        /// <inheritdoc />
        public IDictionary<TKey, IList<NodeInfo>> GetDistribution<TKey>(IReadOnlyDictionary<TKey, ulong> collectionSizes,
            IEnumerable<NodeInfo> nodes)
            where TKey : IEquatable<TKey>, IComparable<TKey>
        {
            var result = new Dictionary<TKey, IList<NodeInfo>>(collectionSizes.Count);
            var orderedNodes= nodes.OrderBy(nodeInfo => nodeInfo.Id).ToList();
            var orderedKeys = collectionSizes
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Select(pair => pair.Key)
                .ToList();

            var count = 0;
            while (count < orderedKeys.Count)
            {
                for (var i = 0; i < orderedNodes.Count && count < orderedKeys.Count; ++i, ++count)
                {
                    result[orderedKeys[count]] = new List<NodeInfo>(2)
                    {
                        orderedNodes[i],
                        orderedNodes[i == orderedNodes.Count - 1 ? 0 : i + 1]
                    };
                }

                for (var i = orderedNodes.Count - 1; i > 0 && count < orderedKeys.Count; --i, ++count)
                {
                    result[orderedKeys[count]] = new List<NodeInfo>(2)
                    {
                        orderedNodes[i],
                        orderedNodes[i == orderedNodes.Count - 1 ? 0 : i + 1]
                    };
                }
            }

            return result;
        }
    }
}