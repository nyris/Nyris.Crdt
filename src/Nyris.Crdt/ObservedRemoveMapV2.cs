using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt
{
    public class ObservedRemoveMapV2<TActorId, TKey, TValue, TValueDto, TValueTimestamp>
        : ObservedRemoveCore<TActorId, ObservedRemoveMapV2<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.MapDeltaItem>
        where TKey : IEquatable<TKey>
        where TValue : class, IDeltaCrdt<TValueDto, TValueTimestamp>, new()
        where TActorId : IEquatable<TActorId>
    {
        // I hate using both lock and concurrent dictionary. However, it's very inconvenient to 
        // constrain all updates to ConcurrentDictionary methods (hence - lock). 
        // But if I were to use normal Dictionary, allowing public enumeration of keys is a pain
        // Proper fully optimized version would look similar to internals of ConcurrentDictionary
        private readonly ConcurrentDictionary<TKey, DottedValue> _items = new();
        // private readonly object _writeLock = new();
        
        // keep everything in array for slightly faster indexing - it is assumed that adding observers
        // is a very rare operation, while notifications happen all the time 
        private IMapObserver<TKey, TValue>[] _observers = Array.Empty<IMapObserver<TKey, TValue>>();
        private readonly object _observersLock = new();

        public int Count => _items.Count;
        public ICollection<TKey> Keys => _items.Keys;
        
        public ImmutableArray<DeltaDto> AddOrMerge(TActorId actorId, TKey key, TValue value)
        {  
            ulong newVersion;
            var oldVersion = (ulong?)null;
            ImmutableArray<TValueDto> valueDeltas;
            MapDeltaItem deltaItem;

            // TODO: is locking really preferable to allocation of closures?
            lock (WriteLock)
            {
                newVersion = GetNewVersion(actorId);
                // if key already exists, we merge provided value with existing one and update dots
                if (_items.TryGetValue(key, out var dottedValue))
                {
                    // merge value, add new version to value's dot list and maybe remove old version (if there is one for this actor)
                    valueDeltas = dottedValue.MergeValueAndUpdateDot(actorId, value, newVersion, out oldVersion);
                    NotifyUpdated(key, dottedValue.Value);
                } 
                else // if the key is new one, simply add the value and get all it's dtos into one array
                {
                    valueDeltas = value.EnumerateDeltaDtos().ToImmutableArray();
                    _items[key] = new DottedValue(value, new DotWithDeltas(actorId, newVersion, valueDeltas));
                    NotifyAdded(key, value);
                }
                
                deltaItem = new MapDeltaItem(key, valueDeltas);
                AddToContextAndInverse(actorId, newVersion, deltaItem);
            }
            
            if (oldVersion.HasValue) RemoveFromInverse(actorId, oldVersion.Value);

            // since we overwrite dots for a value if version is higher, we only need to yield Added dot and may skip the removed one 
            return ImmutableArray.Create(DeltaDto.Added(deltaItem, actorId, newVersion));
        }

        public bool TryRemove(TKey key, out ImmutableArray<DeltaDto> deltas)
        {
            lock (WriteLock)
            {
                if (!_items.TryRemove(key, out var dottedValue))
                {
                    deltas = ImmutableArray<DeltaDto>.Empty;
                    return false;
                }
            
                var dots = dottedValue.InnerDotList;

                var deltasBuilder = ImmutableArray.CreateBuilder<DeltaDto>(dots.Count);
                foreach (var (actor, version, _) in dots)
                {
                    deltasBuilder.Add(DeltaDto.Removed(actor, version));
                    RemoveFromInverse(actor, version);
                }

                NotifyRemoved(key);
                
                deltas = deltasBuilder.MoveToImmutable();
                return true;
            }
        }


        public bool TryMutate(TActorId actorId, TKey key, Func<TValue, ImmutableArray<TValueDto>> func, out ImmutableArray<DeltaDto> delta)
        {
            ImmutableArray<TValueDto> valueDeltas;
            ulong newVersion;
            ulong? oldVersion;
            
            lock (WriteLock)
            {
                if (!_items.TryGetValue(key, out var dottedValue))
                {
                    delta = ImmutableArray<DeltaDto>.Empty;
                    return false;
                }

                newVersion = GetNewVersion(actorId);
                valueDeltas = dottedValue.MutateValueAndUpdateDot(actorId, func, newVersion, out oldVersion);
                NotifyUpdated(key, dottedValue.Value);
            }
            
            var deltaItem = new MapDeltaItem(key, valueDeltas);
            AddToContextAndInverse(actorId, newVersion, deltaItem);
            
            if (oldVersion.HasValue) RemoveFromInverse(actorId, oldVersion.Value);

            delta = ImmutableArray.Create(DeltaDto.Added(deltaItem, actorId, newVersion));
            return true;
        }

        // TODO: how to properly forbid this getter to return reference to TValue?
        public bool TryGet<T>(TKey key, Func<TValue, T> getter, out T? value)
        {
            if (!_items.TryGetValue(key, out var dottedValue))
            {
                value = default;
                return false;
            }

            value = getter(dottedValue.Value);
            return true;
        }
        
        // TODO: updates of retrieved value will lead to lost deltas - how to deal with that?
        public bool TryGet(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            if (!_items.TryGetValue(key, out var dottedValue))
            {
                value = default;
                return false;
            }

            value = dottedValue.Value;
            return true;
        }
        
        public void SubscribeToChanges(IMapObserver<TKey, TValue> observer)
        {
            lock(_observersLock)
            {
                var newArray = new IMapObserver<TKey, TValue>[_observers.Length + 1];
                Array.Copy(_observers, newArray, _observers.Length);
                newArray[_observers.Length] = observer;
                _observers = newArray;
            }
        }

        protected sealed override ulong? AddDot(TActorId actorId, ulong version, MapDeltaItem item)
        {
            var itemKey = item.Key;
            if (_items.TryGetValue(itemKey, out var dottedValue))
            {
                var oldVersion = dottedValue.Merge(actorId, version, item.ValueDeltas);
                NotifyUpdated(itemKey, dottedValue.Value);
                return oldVersion;
            }

            dottedValue = new DottedValue(new TValue());
            dottedValue.Merge(actorId, version, item.ValueDeltas);
            _items[itemKey] = dottedValue;
            NotifyAdded(itemKey, dottedValue.Value);
            return null;
        }

        protected sealed override void RemoveDot(TActorId actorId, ulong version, MapDeltaItem item)
        {
            if (!_items.TryGetValue(item.Key, out var dottedValue)) return;

            dottedValue.RemoveDot(actorId, version);
            if (dottedValue.InnerDotList.Count == 0)
            {
                _items.TryRemove(item.Key, out _);
                NotifyRemoved(item.Key);
            }
        }
        
        private void NotifyAdded(TKey key, TValue value)
        {
            foreach (var observer in _observers) observer.ElementAdded(key, value);
        }
        
        private void NotifyUpdated(TKey key, TValue newValue)
        {
            foreach (var observer in _observers) observer.ElementUpdated(key, newValue);
        }

        private void NotifyRemoved(TKey key)
        {
            foreach (var observer in _observers) observer.ElementRemoved(key);
        }

        
        private readonly struct DotWithDeltas
        {
            public readonly TActorId Actor;
            public readonly ulong Version;
            public readonly ImmutableArray<TValueDto> Deltas;

            public DotWithDeltas(TActorId actor, ulong version, ImmutableArray<TValueDto> deltas)
            {
                Actor = actor;
                Version = version;
                Deltas = deltas;
            }

            public void Deconstruct(out TActorId actor, out ulong version, out ImmutableArray<TValueDto> deltas)
            {
                actor = Actor;
                version = Version;
                deltas = Deltas;
            }
        }

        /// <summary>
        /// See https://www.notion.so/Quest-for-Delta-CRDT-Map-7e97492a57a64a48885d54cd5fe00859#acd5ab9a0f2b4370b01492dce7828873
        /// </summary>
        private sealed class DottedValue
        {
            private bool _conflictingDeltas;
            private readonly List<DotWithDeltas> _dots = new(1);

            public DottedValue(TValue value)
            {
                Value = value;
            }

            public DottedValue(TValue value, DotWithDeltas dot)
            {
                Value = value;
                _dots.Add(dot);
            }
        
            public TValue Value { get; private set; }
            public List<DotWithDeltas> InnerDotList => _dots;

            /// <summary>
            /// Mutates saved value with a provided function <see cref="func"/>. If there exists a dot for this actor, it is overwritten.
            /// TValueDtos in a new dot will contain both deltas which were a result of applying <see cref="func"/> AND all deltas in an already
            /// stored dot of that actor (if any).  
            /// </summary>
            /// <param name="actorId"></param>
            /// <param name="func"></param>
            /// <param name="newVersion"></param>
            /// <param name="oldVersion"></param>
            /// <returns>All deltas of the new dot (result of <see cref="func"/> + any that were stored in a previous version)</returns>
            /// <exception cref="AssumptionsViolatedException">If <see cref="newVersion"/> is less or equal to the one already stored.</exception>
            public ImmutableArray<TValueDto> MutateValueAndUpdateDot(
                TActorId actorId,
                Func<TValue, ImmutableArray<TValueDto>> func,
                ulong newVersion,
                out ulong? oldVersion
            )
            {
                Debug.Assert(newVersion is not 0);
            
                var deltas = func(Value);
                return AddOrUpdateDot(actorId, newVersion, deltas, out oldVersion);
            }
            
            /// <summary>
            /// Merge provided value with stored value. Difference between values (an array: TValueDto[]) is appended to deltas already saved
            /// in a dot of a <see cref="actorId"/> (or saved as is if no dot for <see cref="actorId"/> exists) 
            /// </summary>
            /// <param name="actorId"></param>
            /// <param name="value"></param>
            /// <param name="newVersion"></param>
            /// <param name="oldVersion"></param>
            /// <returns>All deltas of the new dot (result of <see cref="func"/> + any that were stored in a previous version)</returns>
            /// <exception cref="AssumptionsViolatedException">If <see cref="newVersion"/> is less or equal to the one already stored.</exception>
            public ImmutableArray<TValueDto> MergeValueAndUpdateDot(TActorId actorId, TValue value, ulong newVersion, out ulong? oldVersion)
            {
                Debug.Assert(newVersion is not 0);
                Debug.Assert(value is not null);
                
                // get all the deltas from added value, that are new to stored value
                var myTimestamp = Value.GetLastKnownTimestamp();
                var deltas = value.EnumerateDeltaDtos(myTimestamp).ToImmutableArray();
                
                Value.Merge(deltas);
                return AddOrUpdateDot(actorId, newVersion, deltas, out oldVersion);
            }
        
            /// <summary>
            /// Attempts to merge a provided dot. If saved version for a given actor is greater or equal to <see cref="version"/>, nothing happens.
            /// If saved version is less, the dot is overwritten (along with all TValueDtos) 
            /// </summary>
            /// <param name="actorId"></param>
            /// <param name="version"></param>
            /// <param name="deltas"></param>
            /// <returns>An old version if it was removed, null otherwise</returns>
            public ulong? Merge(TActorId actorId, ulong version, ImmutableArray<TValueDto> deltas)
            {
                if (!TryAddOrOverwrite(actorId, version, deltas, out var oldVersion)) return oldVersion;

                try
                {
                    Value.Merge(deltas);
                }
                catch (AssumptionsViolatedException)
                {
                    _conflictingDeltas = true;
                }

                return oldVersion;
            }

            /// <summary>
            /// Removes a dot if exists, does nothing if it dot does not exist.
            /// </summary>
            /// <param name="actorId"></param>
            /// <param name="version"></param>
            public void RemoveDot(TActorId actorId, ulong version)
            {
                var i = FindDot(actorId);
                if (i < 0 || _dots[i].Version != version) return;

                RemoveDotAt(i);
            }

            private void RemoveDotAt(int i)
            {
                var removed = _dots[i];
                _dots.RemoveAt(i);

                // try reverse the removed deltas.
                // After the loop allReversed will be true, if all TryReverse is true AND if conflicting deltas are set to false
                // If allReversed becomes false at some point, abort the loop 
                var allReversed = !_conflictingDeltas;
                var deltas = removed.Deltas;
                for (var j = 0; allReversed && j < deltas.Length; ++j)
                {
                    allReversed &= Value.TryReverse(deltas[j]);
                }

                // if we managed to reverse all deltas - great, all done 
                if (allReversed) return;

                // if some deltas can't be reversed (or if there were conflicting deltas to begin with), we need to recreate the value from all other deltas
                try
                {
                    var newValue = new TValue();
                    foreach (var dot in _dots)
                    {
                        newValue.Merge(dot.Deltas);
                    }

                    Value = newValue;
                    _conflictingDeltas = false;
                }
                catch (AssumptionsViolatedException)
                {
                    _conflictingDeltas = true;
                }
            }

            private int FindDot(TActorId actorId)
            {
                for (var i = 0; i < _dots.Count; ++i)
                {
                    if (_dots[i].Actor.Equals(actorId)) return i;
                }

                return -1;
            }
            
            private bool TryAddOrOverwrite(TActorId actorId, ulong newVersion, ImmutableArray<TValueDto> deltas, out ulong? oldVersion)
            {
                var i = FindDot(actorId);
                if (i >= 0)
                {
                    var savedVersion = _dots[i].Version;
                    if (newVersion <= savedVersion)
                    {
                        oldVersion = default;
                        return false;
                    }

                    // removal and addition in a separate step is intended, you can't just assign _dots[i] = ...
                    // Notice that RemoveDotAt have additional side effects
                    RemoveDotAt(i);
                    _dots.Add(new DotWithDeltas(actorId, newVersion, deltas));
                    oldVersion = savedVersion;
                    return true;
                }
                
                _dots.Add(new DotWithDeltas(actorId, newVersion, deltas));
                oldVersion = default;
                return true;
            }
            
            private ImmutableArray<TValueDto> AddOrUpdateDot(TActorId actorId,
                                                             ulong newVersion,
                                                             ImmutableArray<TValueDto> deltas,
                                                             out ulong? oldVersion)
            {
                var i = FindDot(actorId);
                if (i >= 0)
                {
                    var savedVersion = _dots[i].Version;
                    if (newVersion <= savedVersion)
                    {
                        throw new AssumptionsViolatedException($"DottedValue contains dot ({actorId}, {savedVersion}), that" +
                                                               $"is greater then the new dot ({actorId}, {newVersion}).");
                    }
                    
                    var oldDeltas = _dots[i].Deltas;
                    var newDeltas = ImmutableArray.CreateBuilder<TValueDto>(oldDeltas.Length + deltas.Length);
                    
                    foreach (var delta in oldDeltas) newDeltas.Add(delta);
                    foreach (var delta in deltas) newDeltas.Add(delta);

                    var newDotsDeltas = newDeltas.MoveToImmutable();
                    _dots[i] = new DotWithDeltas(actorId, newVersion, newDotsDeltas);
                    oldVersion = savedVersion;
                    return newDotsDeltas;
                }
                
                _dots.Add(new DotWithDeltas(actorId, newVersion, deltas));
                oldVersion = default;
                return deltas;
            }
        }

        public sealed record MapDeltaItem(TKey Key, ImmutableArray<TValueDto> ValueDeltas);
    }
}
