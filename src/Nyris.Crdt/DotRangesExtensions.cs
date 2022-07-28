using System.Collections.Generic;

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
}
