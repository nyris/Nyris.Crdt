using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Extensions;

namespace Nyris.Crdt.Model
{
    [DebuggerDisplay("{_dict.Count < 5 ? string.Join(',', _dict) : _dict.Count + \" items ...\"}")]
    public sealed class VersionedItemList<TItem> : IEnumerable<ReadOnlyMemory<DottedItem<TItem>>>
        where TItem : IEquatable<TItem>
    {
        private readonly Dictionary<TItem, ulong> _dict = new();
        
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
        
        // Another alternative is to use  SortedDictionary<ulong, TItems>. This gives us O(log n) guarantees on both additions and removals
        // But it looses in enumeration. SortedDictionary does not provide range queries, even though it could.  
        private readonly SortedList<ulong, TItem> _inverse = new();
        private readonly ReaderWriterLockSlim _lock = new();

        private readonly int _enumerationBatchSize;

        // obviously, this is not an actual size (for instance, dictionary has overhead over number of items), but it will do the job
        public ulong StorageSize
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return (ulong) (sizeof(ulong) + Marshal.SizeOf<TItem>()) * (ulong) _dict.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public ICollection<TItem> Items => _dict.Keys;

        public VersionedItemList(int enumerationBatchSize = 10000)
        {
            _enumerationBatchSize = enumerationBatchSize;
        }
        
        public VersionedItemList(IDictionary<TItem, ulong> dict, int enumerationBatchSize = 10000)
        {
            _enumerationBatchSize = enumerationBatchSize;
            _dict = new Dictionary<TItem, ulong>(dict);
            _inverse = new SortedList<ulong, TItem>(dict.ToDictionary(pair => pair.Value, pair => pair.Key));
        }

        public static VersionedItemList<TItem> New() => new();

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
            => TryAdd(item, dot, out _, throwIfNotLatestDot);
        
        public bool TryAdd(TItem item, ulong dot, out ulong? removedDot, bool throwIfNotLatestDot)
        {
            Debug.Assert(item is not null);
            _lock.EnterWriteLock();
            try
            {
                if (_inverse.TryGetValue(dot, out var savedItem) && !savedItem.Equals(item))
                {
                    throw new AssumptionsViolatedException($"Attempt to save an item {item}, to the version history that is already occupied " +
                                                           $"by a different item ({savedItem}, {dot}). This can happen if same ActorId was writing items " +
                                                           "concurrently to different replicas of this set, which is not supported.");
                }
                
                if (!_dict.TryGetValue(item, out var currentDot))
                {
                    _dict[item] = dot;
                    _inverse[dot] = item;
                    removedDot = null;
                    return true;
                }

                if (currentDot >= dot)
                {
                    if (throwIfNotLatestDot)
                    {
                        throw new AssumptionsViolatedException("DottedList contains item with dot, that" +
                                                               "is greater then generated dot.");
                    }

                    removedDot = null;
                    return false; // TODO: think more about result (true/false) meaning and its consistency
                }

                _dict[item] = dot;
                _inverse.Remove(currentDot);
                _inverse[dot] = item;
                removedDot = currentDot;
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

        public bool Contains(in TItem item)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.ContainsKey(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryGetValue(in TItem item, out ulong i) => _dict.TryGetValue(item, out i);
        
        public bool TryRemove(in TItem item, out ulong dot)
        {
            _lock.EnterWriteLock();
            try
            {
                if(!_dict.Remove(item, out dot)) return false;
                _inverse.Remove(dot);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public ArrayBatchEnumerator GetItemsOutsideRanges(ImmutableArray<Range> except) 
            => new(_inverse, _lock, except);

        /// <summary>
        /// Gets dot ranges, for which there are no items saved in this DottedList.
        /// </summary>
        /// <param name="knownRanges">Dots that are not in known ranges will not be returned, even if there are no items
        /// present for them. It is assumed that knownRanges are in increasing order and they do not overlap.</param>
        /// <returns></returns>
        public ImmutableArray<Range> GetEmptyRanges(ImmutableArray<Range> knownRanges)
        {
            _lock.EnterReadLock();
            try
            {
                return _inverse.GetEmptyRanges(knownRanges);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        public bool TryRemove(in Range range)
        {
            _lock.EnterWriteLock();
            try
            {
                var result = true;
                var i = _inverse.GetIndexOfFirstGreaterOrEqualKey(range.From);
                var keys = _inverse.Keys;
                var values = _inverse.Values;
                while(i < keys.Count && keys[i] < range.To)
                {
                    result &= _dict.Remove(values[i]);
                    _inverse.RemoveAt(i); // rare occasion when end of list comes closer to i and not the other way around
                }

                return result;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        public bool TryRemove(ulong dot)
        {
            _lock.EnterWriteLock();
            try
            {
                return _inverse.Remove(dot, out var item) && _dict.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public MemoryBasedEnumerator GetEnumerator() => new(_inverse, _lock, _enumerationBatchSize);

        IEnumerator<ReadOnlyMemory<DottedItem<TItem>>> IEnumerable<ReadOnlyMemory<DottedItem<TItem>>>.GetEnumerator()
            => new MemoryBasedEnumerator(_inverse, _lock, _enumerationBatchSize);
        IEnumerator IEnumerable.GetEnumerator()
            => new MemoryBasedEnumerator(_inverse, _lock, _enumerationBatchSize);
        
        
        public struct ArrayBatchEnumerator : IEnumerator<ArraySegment<DottedItem<TItem>>>, IEnumerable<ArraySegment<DottedItem<TItem>>>
        {
            private readonly int _batchSize;
            private int _length;
            private readonly DottedItem<TItem>[] _batch;
            private readonly SortedList<ulong, TItem> _inverse;
            private readonly ReaderWriterLockSlim _lock;
            private int _versionPosition;
            private int _rangePosition;
            private readonly ImmutableArray<Range> _except;
            
            public ArrayBatchEnumerator(SortedList<ulong, TItem> inverse, ReaderWriterLockSlim @lock, ImmutableArray<Range> except, int batchSize = 1000)
            {
                _inverse = inverse;
                _batchSize = batchSize;
                _except = except;
                _versionPosition = 0;
                _rangePosition = 0;
                _length = 0;
                _lock = @lock;
                _batch = ArrayPool<DottedItem<TItem>>.Shared.Rent(batchSize);
                Reset();
            }

            public bool MoveNext()
            {
                _lock.EnterReadLock();
                try
                {
                    _length = 0;
                    while (_rangePosition < _except.Length && _versionPosition < _inverse.Count)
                    {
                        var nextVersion = _inverse.Keys[_versionPosition];
                        var (from, to) = _except[_rangePosition];
                        if (from > nextVersion) // if 'except' range starts after version, then respected key is of interest to us 
                        {
                            _batch[_length] = new DottedItem<TItem>(_inverse.Values[_versionPosition], nextVersion);
                            ++_versionPosition;
                            ++_length;
                            if (_length < _batchSize) continue;
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
                    if (_versionPosition >= _inverse.Count) return _length > 0;

                    while (_length < _batchSize && _versionPosition < _inverse.Count)
                    {
                        _batch[_length] = new DottedItem<TItem>(_inverse.Values[_versionPosition], _inverse.Keys[_versionPosition]);
                        ++_versionPosition;
                        ++_length;
                    }
                    return _length > 0;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            public ArraySegment<DottedItem<TItem>> Current => new(_batch, 0, _length);

            object IEnumerator.Current => Current;

            public void Reset()
            {
                _lock.EnterReadLock();
                try
                {
                    _versionPosition = _except.Length > 0 && _except[0].From == 1 
                                           ? _inverse.GetIndexOfFirstGreaterOrEqualKey(_except[0].To) 
                                           : 0;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            public void Dispose() => ArrayPool<DottedItem<TItem>>.Shared.Return(_batch);

            public ArrayBatchEnumerator GetEnumerator() => this;
            IEnumerator<ArraySegment<DottedItem<TItem>>> IEnumerable<ArraySegment<DottedItem<TItem>>>.GetEnumerator() => this;
            IEnumerator IEnumerable.GetEnumerator() => this;
        }
        
        public struct MemoryBasedEnumerator : IEnumerator<ReadOnlyMemory<DottedItem<TItem>>>, IEnumerable<ReadOnlyMemory<DottedItem<TItem>>>
        {
            private readonly ReaderWriterLockSlim _lock;
            private readonly int _batchSize;
            private int _length;
            private readonly IMemoryOwner<DottedItem<TItem>> _memoryOwner;
            private readonly SortedList<ulong, TItem> _inverse;
            private int _position;
            private readonly ulong _startDot;

            public MemoryBasedEnumerator(SortedList<ulong, TItem> inverse, ReaderWriterLockSlim @lock, int batchSize, ulong startDot = 0)
            {
                _inverse = inverse;
                _lock = @lock;
                _batchSize = batchSize;
                _startDot = startDot;
                _position = 0;
                _length = 0;
                _memoryOwner = MemoryPool<DottedItem<TItem>>.Shared.Rent(batchSize);
                Reset();
            }

            public bool MoveNext()
            {
                _lock.EnterReadLock();
                try
                {
                    if (_position >= _inverse.Count) return false;

                    var end = Math.Min(_position + _batchSize, _inverse.Count);
                    _length = end - _position;

                    var keys = _inverse.Keys;
                    var values = _inverse.Values;
                    var span = _memoryOwner.Memory.Span;
                    for (var i = 0; i < _length; ++i)
                    {
                        var j = _position + i;
                        span[i] = new DottedItem<TItem>(values[j], keys[j]);
                    }

                    _position = end;
                    return true;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            public ReadOnlyMemory<DottedItem<TItem>> Current => _memoryOwner.Memory[.._length];

            object IEnumerator.Current => Current;

            public void Reset()
            {
                _lock.EnterReadLock();
                try
                {
                    _position = _inverse.GetIndexOfFirstGreaterOrEqualKey(_startDot);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            
            public void Dispose() => _memoryOwner.Dispose();
            MemoryBasedEnumerator GetEnumerator() => this;
            IEnumerator<ReadOnlyMemory<DottedItem<TItem>>> IEnumerable<ReadOnlyMemory<DottedItem<TItem>>>.GetEnumerator() => this;
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}