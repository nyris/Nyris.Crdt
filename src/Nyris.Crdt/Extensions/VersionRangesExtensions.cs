using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Extensions
{
    public static class VersionRangesExtensions
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

        public static InverseEnumerator Inverse(this ImmutableArray<Range> ranges) => new(ranges);

        public struct InverseEnumerator : IEnumerator<Range>, IEnumerable<Range>
        {
            private readonly ImmutableArray<Range> _ranges;
            private ulong _nextFrom = 1;
            private int _i;

            public InverseEnumerator(ImmutableArray<Range> ranges)
            {
                _ranges = ranges;
                _i = 0;
                Current = new Range(0, 1);
            }

            public bool MoveNext()
            {
                if (_i > _ranges.Length) return false;
                while(_i <= _ranges.Length)
                {
                    if (_i == _ranges.Length)
                    {
                        Current = new Range(_nextFrom, long.MaxValue);
                        ++_i;
                        return true;
                    }

                    var current = _ranges[_i];
                    if (_nextFrom >= current.From)
                    {
                        ++_i;
                        continue;
                    }

                    Current = new Range(_nextFrom, current.From);
                    _nextFrom = current.To;
                    ++_i;
                    return true;
                }

                throw new InvalidOperationException("This should never be reached, as loop always returns before finishing");
            }

            public void Reset() => throw new System.NotImplementedException();
            public Range Current { get; private set; }
            object IEnumerator.Current => Current;

            public void Dispose() {}

            public InverseEnumerator GetEnumerator() => this;
            IEnumerator<Range> IEnumerable<Range>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
