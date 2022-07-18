using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Nyris.Crdt.Exceptions;

namespace Nyris.Crdt
{
    public static class DotRangesExtensions
    {
        public static bool IsDisjointAndInIncreasingOrder(this IReadOnlyList<DotRange> ranges)
        {
            if (ranges.Count <= 1) return true;

            for (var i = 1; i < ranges.Count; ++i)
            {
                if (ranges[i - 1].To >= ranges[i].From) return false;
            }

            return true;
        }
    }
    
    [DebuggerDisplay("{_dict.Count < 5 ? string.Join(',', _dict) : _dict.Count + \" items ...\"}")]
    public sealed class DottedList<TItem> : IEnumerable<KeyValuePair<TItem, ulong>>
        where TItem : IEquatable<TItem>
    {
        private readonly ConcurrentDictionary<TItem, ulong> _dict = new();
        
        // Consideration about usage of sorted list:
        //
        // 1. It is the best choice (I think?) for optimizing GetEmptyRanges and GetItems, as it allows access to a list of keys in sorted order 
        // 2. While complexity of adding new elements in general is O(n), but the happy path here is adding elements with
        // ever increasing dots. Which means, majority of additions will be at the end or close to the end,
        // which means actual average addition will be closer to O(log n)
        // 3. Removals are the bad part - we do not have any guarantees on where the removed item is, thus O(n) is the expected complexity
        // 4. Same can be said for adding previously added elements, as this operation requires removal of the old dot.
        //
        // Proper benchmarks are required to check if this choice makes sense in practice.
        // Alternative solution will be to have 2 separate data structures - one Dictionary<ulong, TItems> _inverse for mapping dots to items
        // and something like SortedSet for keeping an order of dots. As all operations are already synchronized by ReaderWriteLockSlim, 
        // there is no additional synchronization downside and additions/removals can be capped as O(log n)
        private readonly SortedList<ulong, TItem> _inverse = new();
        private readonly ReaderWriterLockSlim _lock = new();
        
        public ICollection<TItem> Items => _dict.Keys;

        public DottedList()
        {
        }
        
        public DottedList(IDictionary<TItem, ulong> dict)
        {
            _dict = new ConcurrentDictionary<TItem, ulong>(dict);
            _inverse = new SortedList<ulong, TItem>(dict.ToDictionary(pair => pair.Value, pair => pair.Key));
        }

        public static DottedList<TItem> New() => new();

        public override string ToString()
        {
            _lock.EnterReadLock();
            try
            {
                return string.Join(", ", _inverse.Select(pair => $"({pair.Key}, {pair.Value})"));
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryUpdate(TItem item, ulong newDot, ulong comparisonDot)
        {
            Debug.Assert(item is not null);
            Debug.Assert(newDot != comparisonDot);
            _lock.EnterWriteLock();
            try
            {
                var oldDot = _dict[item];
                if (oldDot != comparisonDot || _inverse.ContainsKey(newDot)) return false;
                
                _dict[item] = newDot;
                _inverse.Remove(oldDot);
                _inverse[newDot] = item;
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        public bool TryAdd(TItem item, ulong dot, bool throwIfNotLatestDot = false)
        {
            Debug.Assert(item is not null);
            _lock.EnterWriteLock();
            try
            {
                if (_inverse.TryGetValue(dot, out var savedItem) && !savedItem.Equals(item))
                {
                    throw new AssumptionsViolatedException("Attempt to save an item to the version history that is already occupied " +
                                                           "by a different item. This can happen if same ActorId was writing items " +
                                                           "concurrently to different replicas of this set, which is not supported.");
                }
                
                if (!_dict.TryGetValue(item, out var currentDot))
                {
                    _dict[item] = dot;
                    _inverse[dot] = item;
                    return true;
                }

                if (currentDot >= dot)
                {
                    if (throwIfNotLatestDot)
                    {
                        throw new AssumptionsViolatedException("DottedList contains item with dot, that" +
                                                               "is greater then generated dot.");
                    }
                    return false; // TODO: think more about result (true/false) meaning and its consistency
                }

                _dict[item] = dot;
                _inverse.Remove(currentDot);
                _inverse[dot] = item;
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetValue(ulong i, [NotNullWhen(true)] out TItem? item)
        {
            _lock.EnterReadLock();
            try
            {
                return _inverse.TryGetValue(i, out item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryGetValue(TItem item, out ulong i) => _dict.TryGetValue(item, out i);
        
        public bool TryRemove(TItem item)
        {
            _lock.EnterWriteLock();
            try
            {
                if(!_dict.TryRemove(item, out var i)) return false;
                _inverse.Remove(i);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public (TItem item, ulong dot)[] GetItemsSince(ulong since)
        {
            _lock.EnterReadLock();
            try
            {
                if (_inverse.Count == 0 || _inverse.Keys[^1] < since) return Array.Empty<(TItem item, ulong dot)>();
                var start = GetFirstDotIndex(since);
                var length = _inverse.Keys.Count - start;
                var result = new (TItem item, ulong dot)[length];
                for (var i = start; i < _inverse.Count; ++i)
                {
                    var dot = _inverse.Keys[i];
                    result[i - start] = new ValueTuple<TItem, ulong>(_inverse[dot], dot);
                }

                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private int GetFirstDotIndex(ulong since)
        {
            var l = 0;
            var r = _inverse.Count - 1;
            var keys = _inverse.Keys;
            
            while (l < r)
            {
                var mid = (l + r) / 2;
                if (keys[mid] == since)
                {
                    return mid;
                }
                else if (keys[mid] > since)
                {
                    r = mid - 1;
                }
                else
                {
                    l = mid + 1;
                }
            }
            
            return l;
        }

        /// <summary>
        /// Gets dot ranges, for which there are no items saved in this DottedList.
        /// </summary>
        /// <param name="knownRanges">Dots that are not in known ranges will not be returned, even if there are no items
        /// present for them. It is assumed that knownRanges are in increasing order and they do not overlap.</param>
        /// <returns></returns>
        public IReadOnlyList<DotRange> GetEmptyRanges(IReadOnlyList<DotRange> knownRanges)
        {
            Debug.Assert(knownRanges.IsDisjointAndInIncreasingOrder());
            if (knownRanges.Count == 0) return Array.Empty<DotRange>();
            var ranges = new List<DotRange>();
            _lock.EnterReadLock();
            try
            {
                var dots = _inverse.Keys;
                if (dots.Count == 0)
                {
                    return knownRanges;
                }

                var knownRangeIndex = 0;
                var knownRange = knownRanges[0];

                if (dots[0] > 1 && knownRange.From < dots[0])
                {
                    ranges.Add(new DotRange(Math.Max(1, knownRange.From), Math.Min(dots[0], knownRange.To)));
                }

                var i = 1;
                while(i < dots.Count)
                {
                    var start = dots[i - 1] + 1;
                    var end = dots[i];
                     
                    if (start == end)
                    {
                        ++i;
                        continue;
                    }

                    if (start < knownRange.To && knownRange.From < end)
                    {
                        ranges.Add(new DotRange(Math.Max(start, knownRange.From), Math.Min(end, knownRange.To)));
                    }

                    if (end <= knownRange.To)
                    {
                        ++i;
                    }
                    else
                    {
                        ++knownRangeIndex;
                        if (knownRangeIndex == knownRanges.Count) return ranges;
                        knownRange = knownRanges[knownRangeIndex];
                    }
                }

                var lastStart = dots[^1] + 1;
                if (lastStart < knownRange.To)
                {
                    ranges.Add(new DotRange(Math.Max(lastStart, knownRange.From), knownRange.To));
                }

                ++knownRangeIndex;
                while (knownRangeIndex < knownRanges.Count)
                {
                    knownRange = knownRanges[knownRangeIndex];
                    if (lastStart < knownRange.To)
                    {
                        ranges.Add(new DotRange(Math.Max(lastStart, knownRange.From), knownRange.To));
                    }
                    ++knownRangeIndex;
                }

                return ranges;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        public bool TryRemove(DotRange range)
        {
            _lock.EnterWriteLock();
            try
            {
                var result = true;
                for (var i = range.From; i < range.To; ++i)
                {
                    result &= _inverse.Remove(i, out var item) && _dict.TryRemove(item, out _);
                }

                return result;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerator<KeyValuePair<TItem, ulong>> GetEnumerator() => _dict.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}