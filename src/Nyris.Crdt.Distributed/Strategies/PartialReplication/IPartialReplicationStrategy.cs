using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies.PartialReplication
{
    public interface IPartialReplicationStrategy
    {
        IDictionary<TKey, IList<NodeInfo>> GetDistribution<TKey>(IReadOnlyDictionary<TKey, ulong> collectionSizes,
            IEnumerable<NodeInfo> nodes)
            where TKey : IEquatable<TKey>, IComparable<TKey>;
    }
}