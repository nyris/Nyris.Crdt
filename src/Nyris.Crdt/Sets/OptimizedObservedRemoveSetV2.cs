using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace Nyris.Crdt.Sets
{
    public class OptimizedObservedRemoveSetV2<TActorId, TItem>
        : ICRDT<OptimizedObservedRemoveSetV2<TActorId, TItem>.Dto>,
          IDeltaCRDT<OptimizedObservedRemoveSetV2<TActorId, TItem>.DeltaDto, Dictionary<TActorId, ulong>>,
          IActoredSet<TActorId, TItem>
        where TItem : IEquatable<TItem>
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
    {
        private readonly ConcurrentDictionary<TActorId, DottedList<TItem>> _items = new();
        private readonly VersionContext<TActorId> _versionContext = new();
        private readonly ReaderWriterLockSlim _lock = new();

        public HashSet<TItem> Values => _items.Values.SelectMany(dict => dict.Items).ToHashSet();

        public void Add(TItem item, TActorId actor)
        {
            Debug.Assert(item is not null);
            Debug.Assert(actor is not null);
            _lock.EnterWriteLock();
            try
            {
                var dot = _versionContext.Increment(actor);
                var actorsDottedItems = _items.GetOrAdd(actor, _ => DottedList<TItem>.New());
                actorsDottedItems.TryAdd(item, dot, true);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Remove(TItem item)
        {
            Debug.Assert(item is not null);
            _lock.EnterWriteLock();
            try
            {
                foreach (var actorsDottedItems in _items.Values)
                {
                    actorsDottedItems.TryRemove(item);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        public MergeResult Merge(Dto other)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var actorId in other.VersionVector.Keys)
                {
                    // if we have not seen this actor, just take everything for this actor from other
                    if (!_versionContext.TryGetValue(actorId, out var myRange))
                    {
                        _items[actorId] = new DottedList<TItem>(other.Items[actorId]);
                        foreach (var range in other.VersionVector[actorId])
                        {
                            _versionContext.Merge(actorId, range);
                        }
                        continue;
                    }

                    var otherItems = other.Items[actorId];
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
                    var otherRanges = new DotRanges(otherRangeList);
                    foreach (var (item, myDot) in myItems)
                    {
                        if (otherRanges.Contains(myDot) && !otherItems.ContainsKey(item))
                        {
                            myItems.TryRemove(item);
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
                                                pair => pair.Value.ToDictionary(p => p.Key, p => p.Value));
                return new Dto(versionVector, items);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public Dictionary<TActorId, ulong> GetLastKnownTimestamp()
        {
            _lock.EnterReadLock();
            try
            {
                return _versionContext.ToDictionary(pair => pair.Value.GetLastBeforeUnknown());
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        public IEnumerable<DeltaDto> EnumerateDeltaDtos(Dictionary<TActorId, ulong>? since = default)
        {
            since ??= new Dictionary<TActorId, ulong>();
            
            foreach (var actorId in _versionContext.Actors)
            {
                if (!TryGetVersion(actorId, out var myDotRanges) || !_items.TryGetValue(actorId, out var myItems)) continue;

                if (!since.TryGetValue(actorId, out var startAt)) startAt = 0;

                // every new item, that was added since provided version
                var newItems = myItems.GetItemsSince(startAt);  // +1 because only deletion can be a new event at startAt  
                for (var i = 0; i < newItems.Length; ++i)
                {
                    var (item, dot) = newItems[i];
                    yield return DeltaDto.Added(item, actorId, dot);
                }

                // every item, that was removed (we don't know the items, but we can restore dots)
                foreach (var range in myItems.GetEmptyRanges(myDotRanges.ToArray())) // faster to copy a short array then to iterate within lock
                {
                    yield return DeltaDto.Removed(actorId, range);
                }
            }
        }

        public void Merge(DeltaDto delta)
        {
            _lock.EnterWriteLock();
            try
            {
                var actorsDottedItems = _items.GetOrAdd(delta.Actor, _ => DottedList<TItem>.New());
                if (delta.IsNewElement)
                {
                    actorsDottedItems.TryAdd(delta.Item, delta.Dot.Value);
                    _versionContext.Merge(delta.Actor, delta.Dot.Value);
                }
                else
                {
                    actorsDottedItems.TryRemove(delta.Range.Value);
                    _versionContext.Merge(delta.Actor, delta.Range.Value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets current version "atomically" - in a sense that it guarantees return will
        /// happen after corresponding item was written (it may have been written long ago and already deleted,
        /// but it will not return in between "write version"-"write item")
        /// </summary>
        /// <param name="actorId"></param>
        /// <param name="dotRange"></param>
        /// <returns></returns>
        private bool TryGetVersion(TActorId actorId, [NotNullWhen(true)] out DotRanges? dotRange)
        {
            _lock.EnterReadLock();
            try
            {
                return _versionContext.TryGetValue(actorId, out dotRange);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        public record Dto(Dictionary<TActorId, DotRange[]> VersionVector, Dictionary<TActorId, Dictionary<TItem, ulong>> Items);

        public record DeltaDto(TItem? Item, TActorId Actor, ulong? Dot, DotRange? Range, ObservationType Type)
        {
            [MemberNotNullWhen(true, nameof(Dot)), 
             MemberNotNullWhen(true, nameof(Item)), 
             MemberNotNullWhen(false, nameof(Range))] 
            public bool IsNewElement => Type == ObservationType.Added;
            
            public static DeltaDto Added(TItem item, TActorId actor, ulong dot) 
                => new(item, actor, dot, null, ObservationType.Added);
            public static DeltaDto Removed(TActorId actor, DotRange range) 
                => new(default, actor, null, range, ObservationType.Removed);
        }
    }
}