using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        public static bool Contains(this ImmutableArray<Range> ranges, ulong version)
        {
            Debug.Assert(ranges.Length > 0);
            var l = 0;
            var r = ranges.Length - 1;

            while (l <= r)
            {
                var mid = (l + r) / 2;
                var midRange = ranges[mid];
                if (midRange.From == version)
                {
                    return true;
                }
                else if (midRange.From > version)
                {
                    r = mid - 1;
                }
                else
                {
                    l = mid + 1;
                }
            }
            // r is now left-closest range
            return r < 0 || r >= ranges.Length || ranges[r].To <= version;
        }
    }
}
