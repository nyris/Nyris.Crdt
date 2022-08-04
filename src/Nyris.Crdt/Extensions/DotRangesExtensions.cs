using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Extensions
{
    public static class DotRangesExtensions
    {
        [Pure]
        public static bool IsDisjointAndInIncreasingOrder(this IReadOnlyList<Range> ranges)
        {
            if (ranges.Count <= 1) return true;

            for (var i = 1; i < ranges.Count; ++i)
            {
                if (ranges[i - 1].To >= ranges[i].From) return false;
            }

            return true;
        }

        [Pure]
        public static Range[] Inverse(this IReadOnlyList<Range> ranges)
        {
            // If first range does not starts at 1, then inverse array will have 1 more element then input array. For example:
            // [3, 5), [6, 8) becomes [1, 3), [5, 6), [8, inf)
            var isFirstSpotEmpty = ranges[0].From > 1;
            var reverse = isFirstSpotEmpty ? new Range[ranges.Count + 1] : new Range[ranges.Count];
            
            if (isFirstSpotEmpty)
            {
                reverse = new Range[ranges.Count + 1];
                reverse[0] = new Range(1, ranges[0].From);
                
                for (var i = 1; i < ranges.Count; ++i)
                {
                    reverse[i] = new Range(ranges[i - 1].To, ranges[i].From);
                }
                
                reverse[^1] = new Range(ranges[^1].To, ulong.MaxValue);
                return reverse;
            }

            // And otherwise, if first range starts at 1, resulting array will be of the same length and we can easily do it in-place
            // for example: [1, 5), [7, 9)  =>  [5, 7), [9, inf)
            for (var i = 0; i < ranges.Count - 1; ++i)
            {
                reverse[i] = new Range(ranges[i].To, ranges[i + 1].From);
            }
                
            reverse[^1] = new Range(ranges[^1].To, ulong.MaxValue);
            return reverse;
        }
    }
}
