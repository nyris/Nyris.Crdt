using System;
using System.Collections.Generic;

namespace Nyris.Crdt.Sets
{
    /// <summary>
    /// A 2-Phase set is a combination of 2 G-Sets, one of whom serves as "tombstones" set.
    /// Elements can be added and removed. But once removed, element can never be added again.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class TwoPhaseTombstoneSet<TItem>
        where TItem : IEquatable<TItem>
    {
        private readonly HashSet<TItem> _addSet = new();

        private readonly HashSet<TItem> _tombstoneSet = new();

        public HashSet<TItem> Set
        {
            get
            {
                var returnSet = new HashSet<TItem>(_addSet, _addSet.Comparer);
                returnSet.ExceptWith(_tombstoneSet);
                return returnSet;
            }
        }

        public void Merge(TwoPhaseTombstoneSet<TItem> other)
        {
            // not check for existence of deleted items in _addSet on purpose. See "Set" property
            _addSet.UnionWith(other._addSet);
            _tombstoneSet.UnionWith(other._tombstoneSet);
        }

        public void Add(TItem item) => _addSet.Add(item);

        public void Remove(TItem item) => _tombstoneSet.Add(item); // remove operation does not touch _addSet on purpose. See "Set" property
    }
}
