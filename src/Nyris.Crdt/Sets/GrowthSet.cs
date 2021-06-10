using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Nyris.Crdt.Sets
{
    /// <summary>
    /// Simple CRDT, defining a Growth-only set
    /// </summary>
    public class GrowthSet<TItem> where TItem : IEquatable<TItem>
    {
        public HashSet<TItem> Set { get; } = new();

        public void Merge(GrowthSet<TItem> other) => Set.UnionWith(other.Set);

        public void Add(TItem item) => Set.Add(item);
    }


    /// <summary>
    /// Distributed counter that can only be incremented
    /// </summary>
    /// <typeparam name="TNodeId"></typeparam>
    public class GCounter<TNodeId> where TNodeId : IEquatable<TNodeId>
    {
        private readonly ConcurrentDictionary<TNodeId, int> _counters = new();

        public int Value => _counters.Values.Sum();

        public void Increment(TNodeId actorId)
            => _counters.AddOrUpdate(actorId, _ => 1, (_, counter) => counter + 1);

        public void Merge(GCounter<TNodeId> other)
        {
            foreach (var nodeId in _counters.Keys.Union(other._counters.Keys))
            {
                other._counters.TryGetValue(nodeId, out var otherValue);
                _counters.AddOrUpdate(nodeId,
                    _ => otherValue,
                    (_, value) => Math.Max(value, otherValue));
            }
        }
    }
}
