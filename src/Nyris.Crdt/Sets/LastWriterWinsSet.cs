using System;
using System.Collections.Generic;
using System.Linq;

namespace Nyris.Crdt.Sets
{
    /// <summary>
    /// LWW-set (Last-Writer-Wins set) is a modification of 2P-Set with timestamps.
    /// Items can be added and removed unlimited amount of times. Each time item is added or removed, it is paired with
    /// current timestamp. On evaluation, item is considered in the set if it is in AddSet and either (1) not in RemoveSet or
    /// (2) in removeSet, but remove timestamp is lower then add timestamp
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class LastWriterWinsSet<TItem> where TItem : IEquatable<TItem>
    {
        private readonly Dictionary<TItem, DateTime> _addSet = new();

        private readonly Dictionary<TItem, DateTime> _removeSet = new();

        public HashSet<TItem> Set => _addSet.Keys
            .Where(item => !_removeSet.ContainsKey(item) || _addSet[item] >= _removeSet[item])
            .ToHashSet();

        public void Merge(LastWriterWinsSet<TItem> other)
        {
            foreach (var key in _addSet.Keys.Union(other._addSet.Keys))
            {
                var mine = _addSet.GetValueOrDefault(key, DateTime.MinValue);
                var others = other._addSet.GetValueOrDefault(key, DateTime.MinValue);
                _addSet[key] = mine > others ? mine : others;
            }

            foreach (var key in _removeSet.Keys.Union(other._removeSet.Keys))
            {
                var mine = _addSet.GetValueOrDefault(key, DateTime.MinValue);
                var others = other._addSet.GetValueOrDefault(key, DateTime.MinValue);
                _removeSet[key] = mine > others ? mine : others;
            }
        }

        public void Add(TItem item) => _addSet[item] = DateTime.UtcNow;

        public void Remove(TItem item) => _removeSet[item] = DateTime.UtcNow;
    }
}
