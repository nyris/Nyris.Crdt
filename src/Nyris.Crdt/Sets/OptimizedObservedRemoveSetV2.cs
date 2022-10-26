using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
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
    [Obsolete("Please use OptimizedObservedRemoveSetV3 instead", false)]
    public class OptimizedObservedRemoveSetV2<TActorId, TItem>
        : SetChangesNotifier<TItem>,
          ICRDT<ObservedRemoveDtos<TActorId, TItem>.Dto>,
          IDeltaCrdt<ObservedRemoveDtos<TActorId, TItem>.DeltaDto, ObservedRemoveDtos<TActorId, TItem>.CausalTimestamp>
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
        public ImmutableArray<ObservedRemoveDtos<TActorId,TItem>.DeltaDto> Add(TItem item, TActorId actor)
        {
            Debug.Assert(item is not null);
            Debug.Assert(actor is not null);
            _lock.EnterWriteLock();
            try
            {
                var itemAlreadyPresent = Contains(item); 
                
                // we add item even if already present, cause of potential concurrent complications
                // For example, think of a case when current item was added long time ago,
                // AND there was a concurrent removal that was not yet propagated.
                // In this case if we do not add an item, once removal is propagated, item will be gone.
                // Checking if item is present in the set before operation is needed only to notify 
                // observers about state change
                
                var version = _versionContext.Increment(actor);
                if (!_items.TryGetValue(actor, out var actorsDottedItems))
                {
                    _items[actor] = actorsDottedItems = VersionedItemList<TItem>.New();
                }
                actorsDottedItems.TryAdd(item, version, out var removedVersion, true);

                if (!itemAlreadyPresent)
                {
                    NotifyAdded(item);
                }
                
                return removedVersion.HasValue 
                           ? ImmutableArray.Create(ObservedRemoveDtos<TActorId,TItem>.DeltaDto.Added(item, actor, version), 
                                                   ObservedRemoveDtos<TActorId,TItem>.DeltaDto.Removed(actor, removedVersion.Value)) 
                           : ImmutableArray.Create(ObservedRemoveDtos<TActorId,TItem>.DeltaDto.Added(item, actor, version));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public ImmutableArray<ObservedRemoveDtos<TActorId,TItem>.DeltaDto> Remove(TItem item)
        {
            Debug.Assert(item is not null);
            _lock.EnterWriteLock(); // although _versionContext is not updated, we need to update multiple dottedLists atomically 
            try
            {
                var dtos = ImmutableArray.CreateBuilder<ObservedRemoveDtos<TActorId,TItem>.DeltaDto>(_items.Count);
                foreach (var (actorId, actorsDottedItems) in _items)
                {
                    if (actorsDottedItems.TryRemove(item, out var removedDot))
                    {
                        dtos.Add(ObservedRemoveDtos<TActorId,TItem>.DeltaDto.Removed(actorId, removedDot));
                    }
                }

                if (dtos.Count > 0)
                {
                    NotifyRemoved(item);
                }
                
                return dtos.ToImmutable();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
#endregion

        #region Crdt
        public MergeResult Merge(ObservedRemoveDtos<TActorId, TItem>.Dto other)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var actorId in other.VersionContext.Keys)
                {
                    var otherItems = other.Items[actorId];
                    
                    // if we have not seen this actor, just take everything for this actor from other
                    if (!_versionContext.TryGetValue(actorId, out var myRange))
                    {
                        var newItems = new List<TItem>();
                        foreach (var item in otherItems.Keys)
                        {
                            if(!Contains(item)) newItems.Add(item);
                        }
                        
                        _items[actorId] = new VersionedItemList<TItem>(otherItems);
                        foreach (var range in other.VersionContext[actorId])
                        {
                            _versionContext.Merge(actorId, range);
                        }

                        foreach (var item in newItems)
                        {
                            NotifyAdded(item);
                        }
                        continue;
                    }

                    var myItems = _items[actorId];

                    // check which items to add or which dots to update
                    foreach (var (item, otherDot) in otherItems)
                    {
                        // if 'other' has items which we have not seen (i.e. their dot is bigger then value from our version vector), take that item 
                        if (!myRange.Contains(otherDot))
                        {
                            var itemInSet = Contains(item); // check if item anywhere in set
                            myItems.TryAdd(item, otherDot);
                            if(!itemInSet) NotifyAdded(item);
                        }
                        // if 'other' has items that we seen and our dot is lower, update the dot
                        else if (myItems.TryGetValue(item, out var myDot) && myDot < otherDot)
                        {
                            myItems.TryUpdate(item, otherDot, myDot);
                        }
                        // if 'other' has items with dot lower then our dot, then we don't need to update - ours is newer
                        // if 'other' has items with a dot lower then our version, but we don't have them - it means we already observed their deletion
                    }

                    var otherRangeList = other.VersionContext[actorId];

                    // check which items to remove
                    var otherRanges = new ConcurrentVersionRanges(otherRangeList);
                    foreach (var batch in myItems)
                    {
                        var span = batch.Span;
                        for (var i = 0; i < batch.Length; ++i)
                        {
                            var item = span[i].Item;
                            if (otherRanges.Contains(span[i].Dot) && !otherItems.ContainsKey(item))
                            {
                                myItems.TryRemove(item, out _);
                                if(!Contains(item)) NotifyRemoved(item);
                            }
                        }
                    }

                    foreach (var range in other.VersionContext[actorId])
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

        public ObservedRemoveDtos<TActorId, TItem>.Dto ToDto()
        {
            _lock.EnterReadLock();
            try
            {
                var versionVector = _versionContext.ToDictionary(pair => pair.Value.ToImmutable());
                var items = _items.ToImmutableDictionary(
                                                pair => pair.Key,
                                                pair => pair.Value
                                                            .SelectMany(batch => batch.ToArray())
                                                            .ToImmutableDictionary(p => p.Item, p => p.Dot));
                return new ObservedRemoveDtos<TActorId, TItem>.Dto(versionVector, items);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
#endregion

        #region DeltaCrdt

        public ObservedRemoveDtos<TActorId,TItem>.CausalTimestamp GetLastKnownTimestamp()
        {
            return new ObservedRemoveDtos<TActorId,TItem>.CausalTimestamp(_versionContext.ToDictionary(pair =>
            {
                var array = pair.Value.ToArray();
                return Unsafe.As<Range[], ImmutableArray<Range>>(ref array);
            }));
        }

        public IEnumerable<ObservedRemoveDtos<TActorId,TItem>.DeltaDto> EnumerateDeltaDtos(ObservedRemoveDtos<TActorId,TItem>.CausalTimestamp? timestamp = default)
        {
            var since = timestamp?.Since ?? ImmutableDictionary<TActorId, ImmutableArray<Range>>.Empty;
            
            foreach (var actorId in _versionContext.Actors)
            {
                if (!_versionContext.TryGetValue(actorId, out var myDotRanges) 
                    || !_items.TryGetValue(actorId, out var myItems)) continue;

                if (!since.TryGetValue(actorId, out var except)) except = ImmutableArray<Range>.Empty;

                // every new item, that was added since provided version
                foreach (var batch in myItems.GetItemsOutsideRanges(except))
                {
                    var innerArray = batch.Array!; // faster indexing
                    for (var i = batch.Offset; i < batch.Count; ++i)
                    {
                        var (item, version) = innerArray[i];
                        yield return ObservedRemoveDtos<TActorId,TItem>.DeltaDto.Added(item, actorId, version);
                    }
                }

                // every item, that was removed (we don't know the items, but we can restore dots)
                var emptyRanges = GetEmptyRanges(myItems, myDotRanges);
                for (var i = 0; i < emptyRanges.Length; ++i)
                {
                    yield return ObservedRemoveDtos<TActorId,TItem>.DeltaDto.Removed(actorId, emptyRanges[i]);
                }
            }
        }

        public DeltaMergeResult Merge(ObservedRemoveDtos<TActorId,TItem>.DeltaDto delta)
        {
            _lock.EnterWriteLock();
            try
            {
                return MergeInternal(delta);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public DeltaMergeResult Merge(ImmutableArray<ObservedRemoveDtos<TActorId,TItem>.DeltaDto> deltas)
        {
            _lock.EnterWriteLock();
            try
            {
                var stateUpdated = false;
                for (var i = 0; i < deltas.Length; ++i)
                {
                    stateUpdated |= DeltaMergeResult.StateUpdated == MergeInternal(deltas[i]);
                }

                return stateUpdated ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private DeltaMergeResult MergeInternal(ObservedRemoveDtos<TActorId,TItem>.DeltaDto delta)
        {
            var actorId = delta.Actor;
            var actorsDottedItems = _items.GetOrAdd(actorId, _ => VersionedItemList<TItem>.New());
            var observedRanges = _versionContext.GetOrAdd(actorId);
            switch (delta)
            {
                case ObservedRemoveDtos<TActorId,TItem>.DeltaDtoAddition(var item, _, var version):
                    // first check if version is new. If already observed by context, ignore  
                    if (observedRanges.Contains(version)) return DeltaMergeResult.StateNotChanged;

                    // item may be also "observed" by another actor - this is important for notifications 
                    var itemAlreadyPresent = Contains(item);
                    
                    actorsDottedItems.TryAdd(item, version);
                    _versionContext.Merge(actorId, version);
                    
                    if(itemAlreadyPresent) NotifyAdded(item);
                    break;
                case ObservedRemoveDtos<TActorId,TItem>.DeltaDtoDeletedDot(_, var version):
                    // state is updated if we removed something, but also if removal failed because we never observed the addition
                    // (in which case VersionContext is updated and Merge returns true)
                    var versionContextUpdated = observedRanges.Merge(version);
                    var itemRemoved = actorsDottedItems.TryRemove(version, out var removedItem);
                    
                    // if item is removed from actorsDottedItems, check if it's present in another actor
                    // if not, then notify observers
                    if (itemRemoved && !Contains(removedItem!)) NotifyRemoved(removedItem!);

                    var stateUpdated = versionContextUpdated || itemRemoved;
                    return stateUpdated ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged;
                case ObservedRemoveDtos<TActorId,TItem>.DeltaDtoDeletedRange(_, var range):
                    stateUpdated = actorsDottedItems.TryRemove(range, out var removedItems) | observedRanges.Merge(range);
                    
                    // this piece is probably the best argument in favor of item-centered design of the set
                    // (in contrast to actor centered used here)
                    if (removedItems is not null)
                    {
                        foreach (var item in removedItems)
                        {
                            if(!Contains(item)) NotifyRemoved(item);
                        }
                    }
                    return stateUpdated ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delta));
            }

            return DeltaMergeResult.StateUpdated;
        }

        private ImmutableArray<Range> GetEmptyRanges(VersionedItemList<TItem> versionedItemList, ConcurrentVersionRanges ranges)
        {
            _lock.EnterReadLock(); // needs to be locked, as reads from both VersionContext and items 
            try
            {
                return versionedItemList.GetEmptyRanges(ranges.ToImmutable());
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

#endregion
    }
}