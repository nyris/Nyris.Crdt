using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Nyris.Crdt.Extensions;

namespace Nyris.Crdt.Model
{
    public sealed class VersionRanges
    {
        private readonly List<Range> _ranges = new();

        public VersionRanges()
        {
        }

        public VersionRanges(IEnumerable<Range> ranges)
        {
            _ranges = ranges as List<Range> ?? ranges.ToList();
            Debug.Assert(_ranges.IsDisjointAndInIncreasingOrder());
        }

        public int Count => _ranges.Count;
        public IReadOnlyList<Range> InnerList => _ranges;

        public override string ToString() => string.Join(", ", _ranges);

        public Range[] ToArray() => _ranges.ToArray();
        public ImmutableArray<Range> ToImmutable() => _ranges.ToImmutableArray();

        public bool Contains(ulong version)
        {
            if (_ranges.Count == 0) return false;
            var i = LeftClosestRangeIndex(version);
            return i >= 0 && _ranges[i].To > version;
        }

        public ulong GetNew()
        {
            if (_ranges.Count == 0)
            {
                _ranges.Add(new Range(1, 2));
                return 1;
            }

            var lastRange = _ranges[^1];
            var nextDot = lastRange.To;
            _ranges[^1] = new Range(lastRange.From, nextDot + 1);
            return nextDot;
        }

        /// <summary>
        /// Merges a given range into a set, preserving ordering and disjointness
        /// </summary>
        /// <param name="range"></param>
        /// <returns>False if no changes were made, true otherwise</returns>
        public bool Merge(Range range)
        {
            if (_ranges.Count == 0)
            {
                _ranges.Add(range);
                return true;
            }

            // Find last range (right-most range) where From <= dot
            var i = LeftClosestRangeIndex(range.From);

            // if there is no saved range that starts before input range
            if (i < 0)
            {
                var first = _ranges[0];

                // if there is no overlap
                if (first.From > range.To)
                {
                    _ranges.Insert(0, range);
                    return true;
                }

                // if we overlap with only the first interval
                if (first.From <= range.To && first.To >= range.To)
                {
                    _ranges[0] = new Range(range.From, first.To);
                    return true;
                }

                // if input range overlaps with first and maybe more intervals

                // find first range that ends later then input range
                // TODO: it may make sense to use binary search here as well (we can do that since ranges are both sorted and disjoint)
                var j = 1;
                while (j < _ranges.Count && range.To >= _ranges[j].To) ++j;

                // input range starts before and ends after all stored ranges
                if (j == _ranges.Count)
                {
                    _ranges.Clear();
                    _ranges.Add(range);
                    return true;
                }

                // input range ends in the existing range
                if (range.To >= _ranges[j].From)
                {
                    _ranges[0] = new Range(range.From, _ranges[j].To);
                    _ranges.RemoveRange(1, j);
                    return true;
                }

                // input range ends before j-th (the end of input range is in between (j-1)th and j-th ranges)
                _ranges[0] = new Range(range.From, range.To);
                if(j > 1) _ranges.RemoveRange(1, j - 1);
                return true;
            }

            // if found range ends after input range, there is no need to do anything
            if (_ranges[i].To >= range.To) return false;

            // if we found last range
            if (i + 1 == _ranges.Count)
            {
                // last range does not intersect with input range
                if (_ranges[i].To < range.From)
                {
                    _ranges.Add(range);
                    return true;
                }

                // last range and input range intersect
                _ranges[i] = new Range(_ranges[i].From, range.To);
                return true;
            }

            // if found range ends before input range starts (we know here that i-th is not the last range)
            if (_ranges[i].To < range.From)
            {
                // find first range that ends after input range
                var j = i + 1;
                while (j < _ranges.Count && range.To >= _ranges[j].To) ++j;

                // input range ends after all existing ranges
                if (j == _ranges.Count)
                {
                    _ranges[i + 1] = new Range(range.From, range.To);
                    // remove [i + 2, Count - 1], length = (Count - 1) - (i + 2) + 1
                    _ranges.RemoveRange(i + 2, _ranges.Count - i - 2);
                    return true;
                }

                // input range ends in the existing range
                if (range.To >= _ranges[j].From)
                {
                    // update (i+1)-th range and remove everything up to (j + 1)-th
                    _ranges[i + 1] = new Range(range.From, _ranges[j].To);
                    // remove [i + 2, j], length = j - (i + 2) + 1
                    if(j > i + 1) _ranges.RemoveRange(i + 2, j - i - 1);
                    return true;
                }

                // input range ends in between (j-1)th and j-th ranges)

                // if we found range right after i-th, the new range does not intersect any saved ranges and just needs to be inserted
                if (j == i + 1)
                {
                    _ranges.Insert(i + 1, range);
                }

                // if we skipped at least one range, overwrite (i+1)th and remove the rest if any
                _ranges[i + 1] = range;
                // remove [i + 2, j - 1], length = (j - 1) - (i + 2) + 1
                if (j > i + 2) _ranges.RemoveRange(i + 2, j - i - 2);
                return true;
            }

            // finally, here we know that: (1) i-th is somewhere in [0, Count - 2]
            // (2) i-th range ends after input range begins

            // find first range that ends after input range
            var k = i + 1;
            while (k < _ranges.Count && range.To >= _ranges[k].To) ++k;

            // input range ends after all existing ranges
            if (k == _ranges.Count)
            {
                _ranges[i] = new Range(_ranges[i].From, range.To);
                // remove [i + 1, Count - 1], length = (Count - 1) - (i + 1) + 1
                _ranges.RemoveRange(i + 1, _ranges.Count - i - 1);
                return true;
            }

            // input range ends in the existing range
            if (range.To >= _ranges[k].From)
            {
                // update i-th range and remove everything up to (k + 1)-th
                _ranges[i] = new Range(_ranges[i].From, _ranges[k].To);
                // remove [i + 1, k], length = k - (i + 1) + 1
                _ranges.RemoveRange(i + 1, k - i);
                return true;
            }

            // input range ends in between (k-1)th and k-th ranges)
            _ranges[i] = new Range(_ranges[i].From, range.To);
            // remove [i + 1, k - 1], length = (k - 1) - (i + 1) + 1
            if (k > i + 1) _ranges.RemoveRange(i + 1, k - i - 1);
            return true;
        }

        public bool TryInsert(ulong dot)
        {
            if (_ranges.Count == 0)
            {
                _ranges.Add(new Range(dot, dot + 1));
                return true;
            }

            // Find last range (right-most range) where From <= dot
            var i = LeftClosestRangeIndex(dot);

            // If i is negative, all ranges are greater then dot being inserted.
            // The only question left - should we create a new range at index 0 or update the 0th range
            if(i < 0)
            {
                var first = _ranges[0];
                if (first.From > dot + 1)
                {
                    _ranges.Insert(0, new Range(dot, dot + 1));
                }
                else
                {
                    _ranges[0] = new Range(dot, first.To);
                }

                return true;
            }

            var prevRange = _ranges[i];
            if (prevRange.To > dot) return false; // Nothing to update, dot was already accounted for.

            // Another edge case - if we found the last range. In this case, we don't need to check next range, only
            // question is - should we append a new range or update the last one.
            if (i + 1 == _ranges.Count)
            {
                if (prevRange.To == dot)
                {
                    _ranges[i] = new Range(prevRange.From, dot + 1);
                }
                else
                {
                    _ranges.Add(new Range(dot, dot + 1));
                }

                return true;
            }

            // Finally - process the case when we need to insert a dot in-between existing ranges.
            var nextRange = _ranges[i + 1];
            if (prevRange.To == dot)  // If new dot is coming right after previous range, we are merging it with it
            {
                // if dot is also right before next range, we are merging previous and next range together
                // (the only situation when _ranges list actually shrinks after merge)
                if (dot + 1 == nextRange.From)
                {
                    _ranges[i] = new Range(prevRange.From, nextRange.To);
                    _ranges.RemoveAt(i + 1);

                    // It is not unexpected to get a lot of separate ranges at first, which are then merged together into
                    // just several large ranges.
                    if (_ranges.Capacity > 2 * _ranges.Count + 1)
                    {
                        _ranges.TrimExcess();
                    }
                    return true;
                }
                _ranges[i] = new Range(prevRange.From, dot + 1);
            }
            else // Dot is not right after previous range...
            {
                // ... but it can be right before next range
                if (dot + 1 == nextRange.From) {
                    _ranges[i + 1] = new Range(dot, nextRange.To);
                    return true;
                }

                // otherwise - simply insert new 1-length range in the appropriate spot
                _ranges.Insert(i + 1, new Range(dot, dot + 1));
            }

            return true;
        }

        private int LeftClosestRangeIndex(ulong dot)
        {
            Debug.Assert(_ranges.Count > 0);
            var l = 0;
            var r = _ranges.Count - 1;

            while (l <= r)
            {
                var mid = (l + r) / 2;
                var midRange = _ranges[mid];
                if (midRange.From == dot)
                {
                    return mid;
                }
                else if (midRange.From > dot)
                {
                    r = mid - 1;
                }
                else
                {
                    l = mid + 1;
                }
            }

            return r;
        }
    }
}
