using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Model;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Sets
{
    /// <summary>
    /// This is a performance-optimized version of <see cref="OptimizedObservedRemoveSet{TActorId,TItem}"/>
    /// It is thread safe with supported concurrent reads. 
    /// </summary>
    /// <typeparam name="TActorId"></typeparam>
    /// <typeparam name="TItem"></typeparam>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Performance")]
    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery", Justification = "Performance")]
    public class OptimizedObservedRemoveSetV2<TActorId, TItem>
        : ICRDT<OptimizedObservedRemoveSetV2<TActorId, TItem>.Dto>,
          IDeltaCrdt<OptimizedObservedRemoveSetV2<TActorId, TItem>.DeltaDto, Dictionary<TActorId, ulong>>
        where TItem : IEquatable<TItem>
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
    {
        private readonly ConcurrentDictionary<TActorId, VersionedItemList<TItem>> _items = new();
        private readonly VersionContext<TActorId> _versionContext = new();
        
        // lock is used for operations, that rely on _items and _versionContext being in sync 
        private readonly ReaderWriterLockSlim _lock = new();
        
        public HashSet<TItem> Values
        {
            get
            {
                var values = new HashSet<TItem>();
                foreach (var batch in _items.Values.SelectMany(dottedList => dottedList))
                {
                    var span = batch.Span;
                    for (var i = 0; i < span.Length; ++i)
                    {
                        values.Add(span[i].Item);
                    }
                }

                return values;
            }
        }

        // TODO: maybe optimize this to be updated-on-write and just a constant lookup on read
        public ulong StorageSize => _items.Values.Aggregate(_versionContext.StorageSize, (current, dottedList) => current + dottedList.StorageSize);

        public bool Contains(TItem item)
        {
            foreach (var dottedList in _items.Values)
            {
                if (dottedList.Contains(item)) return true;
            }
            return false;
        }

        #region Mutations
        public IReadOnlyList<DeltaDto> Add(TItem item, TActorId actor)
        {
            Debug.Assert(item is not null);
            Debug.Assert(actor is not null);
            _lock.EnterWriteLock();
            try
            {
                var dot = _versionContext.Increment(actor);
                if (!_items.TryGetValue(actor, out var actorsDottedItems))
                {
                    _items[actor] = actorsDottedItems = VersionedItemList<TItem>.New();
                }
                actorsDottedItems.TryAdd(item, dot, out var removedDot, true);
                return removedDot.HasValue 
                           ? new[] { DeltaDto.Added(item, actor, dot), DeltaDto.Removed(actor, removedDot.Value) } 
                           : new[] { DeltaDto.Added(item, actor, dot) };
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IReadOnlyList<DeltaDto> Remove(TItem item)
        {
            Debug.Assert(item is not null);
            _lock.EnterWriteLock(); // although _versionContext is not updated, we need to update multiple dottedLists atomically 
            try
            {
                var dtos = new List<DeltaDto>(_items.Count);
                foreach (var (actorId, actorsDottedItems) in _items)
                {
                    if (actorsDottedItems.TryRemove(item, out var removedDot))
                    {
                        dtos.Add(DeltaDto.Removed(actorId, removedDot));
                    }
                }

                return dtos;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
#endregion

        #region Crdt
        public MergeResult Merge(Dto other)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var actorId in other.VersionVector.Keys)
                {
                    var otherItems = other.Items[actorId];
                    
                    // if we have not seen this actor, just take everything for this actor from other
                    if (!_versionContext.TryGetValue(actorId, out var myRange))
                    {
                        _items[actorId] = new VersionedItemList<TItem>(otherItems);
                        foreach (var range in other.VersionVector[actorId])
                        {
                            _versionContext.Merge(actorId, range);
                        }
                        continue;
                    }

                    var myItems = _items[actorId];

                    // check which items to add or which dots to update
                    foreach (var (item, otherDot) in otherItems)
                    {
                        // if 'other' has items with we have not seen (i.e. their dot is bigger then value from our version vector), take that item 
                        if (!myRange.Contains(otherDot))
                        {
                            myItems.TryAdd(item, otherDot);
                        }
                        // if 'other' has items that we seen and our dot is lower, update the dot
                        else if (myItems.TryGetValue(item, out var myDot) && myDot < otherDot)
                        {
                            myItems.TryUpdate(item, otherDot, myDot);
                        }
                        // if 'other' has items with dot lower then our dot, then we don't need to update - ours is newer
                        // if 'other' has items with a dot lower then our version, but we don't have them - it means we already observed their deletion
                    }

                    var otherRangeList = other.VersionVector[actorId];

                    // check which items to remove
                    var otherRanges = new ConcurrentVersionRanges(otherRangeList);
                    foreach (var batch in myItems)
                    {
                        var span = batch.Span;
                        for (var i = 0; i < batch.Length; ++i)
                        {
                            if (otherRanges.Contains(span[i].Dot) && !otherItems.ContainsKey(span[i].Item))
                            {
                                myItems.TryRemove(span[i].Item, out _);
                            }
                            
                        }
                    }

                    foreach (var range in other.VersionVector[actorId])
                    {
                        _versionContext.Merge(actorId, range);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return MergeResult.ConflictSolved;
        }

        public Dto ToDto()
        {
            _lock.EnterReadLock();
            try
            {
                var versionVector = _versionContext.ToDictionary();
                var items = _items.ToDictionary(
                                                pair => pair.Key,
                                                pair => pair.Value
                                                            .SelectMany(batch => batch.ToArray())
                                                            .ToDictionary(p => p.Item, p => p.Dot));
                return new Dto(versionVector, items);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
#endregion

        #region DeltaCrdt

        public Dictionary<TActorId, ulong> GetLastKnownTimestamp() 
            // no locking is necessary as both VersionContext and DottedList is already thread safe on it's own 
            => _versionContext.ToDictionary(pair => pair.Value.GetFirstUnknown());

        public IEnumerable<DeltaDto> EnumerateDeltaDtos(Dictionary<TActorId, ulong>? timestamp = default)
        {
            timestamp ??= new Dictionary<TActorId, ulong>();
            
            foreach (var actorId in _versionContext.Actors)
            {
                if (!_versionContext.TryGetValue(actorId, out var myDotRanges) 
                    || !_items.TryGetValue(actorId, out var myItems)) continue;

                if (!timestamp.TryGetValue(actorId, out var startAt)) startAt = 0;

                // every new item, that was added since provided version
                foreach (var batch in myItems.GetItemsSince(startAt))
                {
                    var innerArray = batch.Array!; // faster indexing
                    for (var i = batch.Offset; i < batch.Count; ++i)
                    {
                        var (item, dot) = innerArray[i];
                        yield return DeltaDto.Added(item, actorId, dot);
                    }
                }

                // every item, that was removed (we don't know the items, but we can restore dots)
                var emptyRanges = GetEmptyRanges(myItems, myDotRanges);
                for (var i = 0; i < emptyRanges.Count; ++i)
                {
                    yield return DeltaDto.Removed(actorId, emptyRanges[i]);
                }
            }
        }

        public IReadOnlyList<DeltaDto> ProduceDeltasFor(TItem item)
        {
            var result = new List<DeltaDto>(_items.Count);
            foreach (var (actorId, dottedList) in _items)
            {
                if (dottedList.TryGetValue(item, out var dot))
                {
                    result.Add(DeltaDto.Added(item, actorId, dot));
                }
            }

            return result;
        }

        public void Merge(DeltaDto delta)
        {
            _lock.EnterWriteLock();
            try
            {
                MergeInternal(delta);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Merge(IReadOnlyList<DeltaDto> deltas)
        {
            _lock.EnterWriteLock();
            try
            {
                for (var i = 0; i < deltas.Count; ++i)
                {
                    MergeInternal(deltas[i]);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void MergeInternal(DeltaDto delta)
        {
            if (!_items.TryGetValue(delta.Actor, out var actorsDottedItems))
            {
                _items[delta.Actor] = actorsDottedItems = VersionedItemList<TItem>.New();
            }
            switch (delta)
            {
                case DeltaDtoAddition(var item, _, var version):
                    actorsDottedItems.TryAdd(item, version);
                    _versionContext.Merge(delta.Actor, version);
                    break;
                case DeltaDtoDeletedDot(var actorId, var version):
                    actorsDottedItems.TryRemove(version);
                    _versionContext.Merge(actorId, version);
                    break;
                case DeltaDtoDeletedRange(var actorId, var range):
                    actorsDottedItems.TryRemove(range);
                    _versionContext.Merge(actorId, range);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delta));
            }
        }

        private IReadOnlyList<Range> GetEmptyRanges(VersionedItemList<TItem> versionedItemList, ConcurrentVersionRanges ranges)
        {
            _lock.EnterReadLock(); // needs to be locked, as reads from both VersionContext and items 
            try
            {
                return versionedItemList.GetEmptyRanges(ranges.ToArray());
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

#endregion

        #region DtoRecords

        public record Dto(Dictionary<TActorId, Range[]> VersionVector, Dictionary<TActorId, Dictionary<TItem, ulong>> Items);

        public abstract record DeltaDto(TActorId Actor)
        {
            public static DeltaDto Added(TItem item, TActorId actor, ulong version) 
                => new DeltaDtoAddition(item, actor, version);
            public static DeltaDto Removed(TActorId actor, Range range) 
                => new DeltaDtoDeletedRange(actor, range);
            public static DeltaDto Removed(TActorId actor, ulong version) 
                => new DeltaDtoDeletedDot(actor, version);
        }
        
        public sealed record DeltaDtoAddition(TItem Item, TActorId Actor, ulong Version) : DeltaDto(Actor);
        public sealed record DeltaDtoDeletedDot(TActorId Actor, ulong Version) : DeltaDto(Actor);
        public sealed record DeltaDtoDeletedRange(TActorId Actor, Range Range) : DeltaDto(Actor);

#endregion
    }
}