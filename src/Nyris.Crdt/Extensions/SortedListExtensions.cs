
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Extensions
{
    public static class SortedListExtensions
    {
        /// <summary>
        /// Finds index of a first key in the list that is greater or equal to provided one.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="key"></param>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <returns>Index of the first list.Key that is greater or equal to <param name="key"/> or list.Count if there are no such key.</returns>
        [Pure]
        public static int GetIndexOfFirstGreaterOrEqualKey<TKey, TValue>(this SortedList<TKey, TValue> list, TKey key)
            where TKey : IComparable<TKey>
        {
            // if (list.Count == 0 || list.Keys[0].CompareTo(key) >= 0) return 0;
            // if (list.Keys[^1].CompareTo(key) < 0) return list.Count;
            
            var l = 0;
            var r = list.Count - 1;
            var keys = list.Keys;

            while (l <= r)
            {
                var mid = (l + r) / 2;
                switch (keys[mid].CompareTo(key))  // in case you are wondering like I was - using CompareTo somehow 
                {                                  // turns out to be ~10% faster than using >, <, == operators  
                    case 0:
                        return mid;
                    case > 0:
                        r = mid - 1;
                        break;
                    default:
                        l = mid + 1;
                        break;
                }
            }
            
            return l;
        }

        public static ImmutableArray<Range> GetEmptyRanges<T>(this SortedList<ulong, T> inverse, ImmutableArray<Range> knownRanges)
        {
            Debug.Assert(knownRanges.IsDisjointAndInIncreasingOrder());
            if (knownRanges.Length == 0) return ImmutableArray<Range>.Empty;
            
            var ranges = ImmutableArray.CreateBuilder<Range>();
            var versions = inverse.Keys;
            if (versions.Count == 0)
            {
                return knownRanges;
            }

            var knownRangeIndex = 0;
            var knownRange = knownRanges[0];

            if (versions[0] > 1 && knownRange.From < versions[0])
            {
                ranges.Add(new Range(Math.Max(1, knownRange.From), Math.Min(versions[0], knownRange.To)));
            }

            var i = 1;
            while(i < versions.Count)
            {
                var start = versions[i - 1] + 1;
                var end = versions[i];
                 
                if (start == end)
                {
                    ++i;
                    continue;
                }

                if (start < knownRange.To && knownRange.From < end)
                {
                    ranges.Add(new Range(Math.Max(start, knownRange.From), Math.Min(end, knownRange.To)));
                }

                if (end <= knownRange.To)
                {
                    ++i;
                }
                else
                {
                    ++knownRangeIndex;
                    if (knownRangeIndex == knownRanges.Length) return ranges.ToImmutable();
                    knownRange = knownRanges[knownRangeIndex];
                }
            }

            var lastStart = versions[^1] + 1;
            if (lastStart < knownRange.To)
            {
                ranges.Add(new Range(Math.Max(lastStart, knownRange.From), knownRange.To));
            }

            ++knownRangeIndex;
            while (knownRangeIndex < knownRanges.Length)
            {
                knownRange = knownRanges[knownRangeIndex];
                if (lastStart < knownRange.To)
                {
                    ranges.Add(new Range(Math.Max(lastStart, knownRange.From), knownRange.To));
                }
                ++knownRangeIndex;
            }

            return ranges.ToImmutable();
        }
    }
}
