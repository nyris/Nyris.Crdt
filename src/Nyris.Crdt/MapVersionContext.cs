using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Nyris.Crdt.Extensions;
using Nyris.Crdt.Model;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt
{
    [DebuggerDisplay("{DebuggerDisplayString,nq}")]
    internal sealed class MapVersionContext<TKey> : IDisposable
        where TKey : IEquatable<TKey>
    {
        private readonly VersionRanges _ranges = new();
        private readonly SortedList<ulong, TKey> _inverse = new();
        private readonly ReaderWriterLockSlim _lock = new();

        public void UpdateVersion(TKey key, ulong newVersion, ulong oldVersion)
        {
            _lock.EnterWriteLock();
            try
            {
                _ranges.TryInsert(newVersion);
                _inverse.Remove(oldVersion, out var removedKey);
                Debug.Assert(key.Equals(removedKey));
                _inverse[newVersion] = key;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public TKey? ObserveAndClear(ulong version, out bool newVersionWasInserted)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!(newVersionWasInserted = _ranges.TryInsert(version))) return default;
                return _inverse.Remove(version, out var key) ? key : default;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public ImmutableArray<(TKey Key, ulong Version)> ObserveAndClear(Range range, out bool newVersionsWasInserted)
        {
            _lock.EnterWriteLock();
            try
            {
                newVersionsWasInserted = _ranges.Merge(range);
                var i = _inverse.GetIndexOfFirstGreaterOrEqualKey(range.From);
                var end = _inverse.GetIndexOfFirstGreaterOrEqualKey(range.To);
                var keys = _inverse.Keys;
                var values = _inverse.Values;
                var removedKeys = ImmutableArray.CreateBuilder<(TKey Key, ulong Version)>(end - i);
                while(i < keys.Count && keys[i] < range.To)
                {
                    removedKeys.Add((values[i], keys[i]));
                    _inverse.RemoveAt(i); // rare occasion when end of list comes closer to i and not the other way around
                }

                return removedKeys.MoveToImmutable();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void MergeVersion(ulong version)
        {
            _lock.EnterWriteLock();
            try
            {
                _ranges.TryInsert(version);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryInsert(TKey key, ulong version)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_ranges.TryInsert(version)) return false;
                _inverse[version] = key;
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public ulong GetNewVersion(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                var newVersion = _ranges.GetNew();
                _inverse[newVersion] = key;
                return newVersion;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        [Pure]
        public ImmutableArray<Range> GetEmptyRanges()
        {
            _lock.EnterReadLock();
            try
            {
                return _inverse.GetEmptyRanges(_ranges.ToImmutable());
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        [Pure]
        public ImmutableArray<Range> GetRanges()
        {
            _lock.EnterReadLock();
            try
            {
                var array = _ranges.ToArray();
                return Unsafe.As<Range[], ImmutableArray<Range>>(ref array);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void MaybeClearVersion(ulong? oldVersion)
        {
            if (!oldVersion.HasValue) return;
            ClearVersion(oldVersion.Value);
        }

        public void ClearVersion(ulong oldVersion)
        {
            _lock.EnterWriteLock();
            try
            {
                _inverse.Remove(oldVersion);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        public Enumerator EnumerateKeysOutsideRanges(ImmutableArray<Range> except) => new(except, _inverse, _lock);

        public struct Enumerator : IEnumerator<TKey>
        {
            private readonly ImmutableArray<Range> _except;
            private int _rangePosition;
            private int _versionPosition;
            private readonly SortedList<ulong, TKey> _inverse;
            private readonly ReaderWriterLockSlim _lock;

            public Enumerator(ImmutableArray<Range> except, SortedList<ulong, TKey> inverse, ReaderWriterLockSlim @lock)
            {
                _except = except;
                _inverse = inverse;
                _lock = @lock;
                _versionPosition = 0;
                _rangePosition = 0;
                Current = default!;
            }

            public bool MoveNext()
            {
                _lock.EnterReadLock();
                try
                {
                    while (_rangePosition < _except.Length && _versionPosition < _inverse.Count)
                    {
                        var nextVersion = _inverse.Keys[_versionPosition];
                        var (from, to) = _except[_rangePosition];
                        if (from > nextVersion) // if 'except' range starts after version, then respected key is of interest to us
                        {
                            Current = _inverse.Values[_versionPosition];
                            ++_versionPosition;
                            return true;
                        }

                        if (to > nextVersion) // if nextVersion is within one of 'except' ranges, it should be skipped - advance version pointer and try again
                        {
                            ++_versionPosition;
                        }
                        else // if nextVersion lies beyond current range, we need to check if nextRange does not apply - advance range pointer and try again
                        {
                            ++_rangePosition;
                        }
                    }

                    // check if there no versions left
                    if (_versionPosition >= _inverse.Count) return false;

                    Current = _inverse.Values[_versionPosition];
                    ++_versionPosition;
                    return true;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            public void Reset() => _rangePosition = _versionPosition = 0;

            public TKey Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                // nothing to dispose
            }

            public Enumerator GetEnumerator() => this;
        }

        private string DebuggerDisplayString => _ranges.ToString();
    }
}
