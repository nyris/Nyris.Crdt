using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Model;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt
{
    public class ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp> 
        : IDeltaCrdt<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.DeltaDto, 
            ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.CausalTimestamp>
        where TKey : IEquatable<TKey>
        where TValue : class, IDeltaCrdt<TValueDto, TValueTimestamp>, new()
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
        where TValueTimestamp : IComparable<TValueTimestamp>
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly ConcurrentDictionary<TKey, DottedValue> _items = new();
        private readonly ConcurrentDictionary<TActorId, MapVersionContext<TActorId, TKey, TValueTimestamp>> _context = new();
        
        public DeltaDto AddOrMerge(TActorId actorId, TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                var context = _context.GetOrAdd(actorId, _ => new MapVersionContext<TActorId, TKey, TValueTimestamp>());
                var newVersion = context.GetNewVersion(key);

                // if key already exists, we merge provided value with existing one and update dots
                if (_items.TryGetValue(key, out var dottedValue))
                {
                    // merge value, add new version to value's dot list and maybe remove old version (if there is one for this actor)
                    var valueDeltas = dottedValue.MergeValueAndUpsertDot(actorId, value, newVersion, out var oldVersion);

                    // generate deltas for added/removed dots
                    var keysDeltas = GetKeyDeltas(actorId, key, newVersion, oldVersion);
                    
                    // if old version was removed, we can drop it from inverse map
                    context.MaybeClearVersion(oldVersion);
                    return new DeltaDto(keysDeltas, valueDeltas, key);
                }
                else // if the key is new one, simply add the value and get all it's dtos into one array
                {
                    _items[key] = new DottedValue(value ,new Dot<TActorId>(actorId, newVersion));
                    var keysDeltas = GetKeyDeltas(actorId, key, newVersion);
                    var valueDtos = value.EnumerateDeltaDtos().ToArray();
                    return new DeltaDto(keysDeltas, valueDtos, key);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryRemove(TKey key, [NotNullWhen(true)] out DeltaDto? delta)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_items.TryRemove(key, out var dottedValue))
                {
                    delta = default;
                    return false;
                }

                var dots = dottedValue.Dots;
                var keysDelta = new KeyDeltaDto[dots.Count];
                for (var i = 0; i < keysDelta.Length; ++i)
                {
                    var (actor, version) = dots[i];
                    keysDelta[i] = KeyDeltaDto.Removed(actor, version);
                    _context[actor].ClearVersion(version);
                }

                delta = new DeltaDto(keysDelta, Array.Empty<TValueDto>(), key);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        
        public bool TryMutate(TActorId actorId, 
                              TKey key, 
                              Func<TValue, TValueDto[]> func, 
                              [NotNullWhen(true)] out DeltaDto? delta)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_items.TryGetValue(key, out var dottedValue))
                {
                    delta = default;
                    return false;
                }
                
                var context = _context[actorId];
                var newVersion = context.GetNewVersion(key);

                var valueDeltas = dottedValue.MutateValueAndUpsertDot(actorId, func, newVersion, out var oldVersion);
                var keysDeltas = GetKeyDeltas(actorId, key, newVersion, oldVersion);
                context.MaybeClearVersion(oldVersion);
                
                delta = new DeltaDto(keysDeltas, valueDeltas, key);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
 
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

        public CausalTimestamp GetLastKnownTimestamp()
        {
            var since = _context.ToDictionary(pair => pair.Key, pair => pair.Value.GetRanges());
            return _items.Count < 10000
                       ? new CausalTimestamp(since, _items.ToDictionary(pair => pair.Key, pair => pair.Value.Value.GetLastKnownTimestamp()))
                       : new CausalTimestamp(since, ImmutableDictionary<TKey, TValueTimestamp>.Empty);
        }

        public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? timestamp = default)
        {
            var since = timestamp?.Since ?? ImmutableDictionary<TActorId, Range[]>.Empty;
            var valueTimestamps = timestamp?.ValueTimestamps ?? ImmutableDictionary<TKey, TValueTimestamp>.Empty;

            // 'remove' deltas - keys that were removed. Include all known removals, as we do not pass info on what is known about removals
            foreach (var (actor, context) in _context)
            {
                var emptyRanges = context.GetEmptyRanges();
                // TODO: batch those? Is there a scenario when a number of empty ranges is too large?
                var keyDeltas = GetKeyDeltas(actor, emptyRanges);
                yield return new DeltaDto(keyDeltas, Array.Empty<TValueDto>(), default);
            }

            // 'add' deltas - keys and values that were added/updated after provided timestamp.
            var seenKeys = new HashSet<TKey>(_items.Count);
            foreach (var (actor, context) in _context)
            {
                if (!since.TryGetValue(actor, out var ranges)) ranges = Array.Empty<Range>();
                foreach (var key in context.EnumerateKeysOutsideRanges(ranges))
                {
                    if (!seenKeys.Add(key) || !_items.TryGetValue(key, out var dottedValue)) continue;
                    valueTimestamps.TryGetValue(key, out var valueTimestamp);
                    // TODO: value dtos definitely may be too large, need to batch them
                    yield return dottedValue.ToDto(key, valueTimestamp);
                }
            }
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

        private void MergeInternal(DeltaDto delta)
        {
            var (keysDeltas, valueDeltas, key) = delta;
            if (key is not null)
            {
                var dottedValue = _items.GetOrAdd(key, _ => new DottedValue(new TValue()));
                dottedValue.Value.Merge(valueDeltas);
                
                foreach (var deltaDto in keysDeltas)
                {
                    MergeKeyDot(deltaDto, dottedValue);
                }

                if (dottedValue.Dots.Count > 0) return;

                // if after all merges dottedValue contains zero dots, then it can be removed
                _items.TryRemove(key, out _);
            }
            else // delta covers unknown number of keys - those only happen when we pass removed ranges
            {
                var allDeltas = delta.KeysDeltas;
                Debug.Assert(allDeltas.All(d => d is KeyDeltaDtoDeletedRange));
                var deltas = Unsafe.As<KeyDeltaDto[], KeyDeltaDtoDeletedRange[]>(ref allDeltas);
                
                foreach (var deltaDto in deltas)
                {
                    var context = _context.GetOrAdd(deltaDto.Actor, _ => new MapVersionContext<TActorId, TKey, TValueTimestamp>());
                    var keys = context.ObserveAndClear(deltaDto.Range);
                    foreach (var keyToRemove in keys)
                    {
                        _items.TryRemove(keyToRemove, out _);
                    }
                }
            }
        }
        
        private void MergeKeyDot(KeyDeltaDto delta, DottedValue dottedValue)
        {
            var dots = dottedValue.Dots;
            var context = _context.GetOrAdd(delta.Actor, _ => new MapVersionContext<TActorId, TKey, TValueTimestamp>());
            switch (delta)
            {
                case KeyDeltaDtoAddition(var key, var actorId, var otherVersion):
                    // check if this actor is already present in dot list. If saved version for it is lower than delta's, update the dot
                    for (var i = 0; i < dots.Count; ++i)
                    {
                        var (actor, myVersion) = dots[i];
                        if (!actor.Equals(actorId)) continue;
                        if (myVersion >= otherVersion) return;

                        context.UpdateVersion(key, otherVersion, myVersion);
                        dots[i] = new Dot<TActorId>(actorId, otherVersion);
                        return;
                    }
                    // if we have not found this actor, try to insert into context. False means we already observed this dot
                    if (!context.TryInsert(key, otherVersion)) break;
                    
                    dots.Add(new Dot<TActorId>(actorId, otherVersion));
                    break;
                case KeyDeltaDtoDeletedDot(var actorId, var otherVersion):
                    // check if this actor is already present in dot list. If saved version is not greater than delta's, remove the dot
                    for (var i = 0; i < dots.Count; ++i)
                    {
                        var (actor, myVersion) = dots[i];
                        if (!actor.Equals(actorId)) continue;
                        if (myVersion > otherVersion) break; // old removal, we know that value was added again later
                        
                        context.ClearVersion(myVersion);
                        dots.RemoveAt(i);
                        break;
                    }
                    // finally make sure context have record of this dot
                    context.MergeVersion(otherVersion);
                    break;
                case KeyDeltaDtoDeletedRange:
                    throw new AssumptionsViolatedException("Delta for removing range of dots can not be applied to a single dottedValue");
                default:
                    throw new ArgumentOutOfRangeException(nameof(delta));
            }
        }
        
        private static KeyDeltaDto[] GetKeyDeltas(TActorId actor, IReadOnlyList<Range> emptyRanges)
        {
            var keyDeltas = new KeyDeltaDto[emptyRanges.Count];
            for (var i = 0; i < emptyRanges.Count; ++i)
            {
                keyDeltas[i] = KeyDeltaDto.Removed(actor, emptyRanges[i]);
            }

            return keyDeltas;
        }
        
        private static KeyDeltaDto[] GetKeyDeltas(
            TActorId actorId,
            TKey key,
            ulong version) => new KeyDeltaDto[] { new KeyDeltaDtoAddition(key, actorId, version) };
        
        private static KeyDeltaDto[] GetKeyDeltas(
            TActorId actorId,
            TKey key,
            ulong newVersion,
            ulong? oldVersion) 
            => oldVersion.HasValue
                   ? new[]
                   {
                       KeyDeltaDto.Added(key, actorId, newVersion),
                       KeyDeltaDto.Removed(actorId, oldVersion.Value)
                   }
                   : new[] { KeyDeltaDto.Added(key, actorId, newVersion) };
        
        
        private sealed class DottedValue
        {
            public DottedValue(TValue value)
            {
                Value = value;
            }
            public DottedValue(TValue value, Dot<TActorId> dot)
            {
                Value = value;
                Dots.Add(dot);
            }

            public List<Dot<TActorId>> Dots { get; } = new(1);
            public TValue Value { get; }

            [Pure]
            public DeltaDto ToDto(TKey key, TValueTimestamp? valueTimestamp = default)
            {
                var keyDeltas = new KeyDeltaDto[Dots.Count];
                for (var i = 0; i < Dots.Count; ++i)
                {
                    var (dotActor, version) = Dots[i];
                    keyDeltas[i] = KeyDeltaDto.Added(key, dotActor, version);
                }

                var valueTimestamps = Value.EnumerateDeltaDtos(valueTimestamp).ToArray();
                return new DeltaDto(keyDeltas, valueTimestamps, key);
            }
            
            public TValueDto[] MutateValueAndUpsertDot(
                TActorId actorId,
                Func<TValue, TValueDto[]> func,
                ulong newVersion,
                out ulong? oldVersion
            )
            {
                oldVersion = UpsertDot(actorId, newVersion);
                return func(Value);
            }
            
            public TValueDto[] MergeValueAndUpsertDot(TActorId actorId, TValue value, ulong newVersion, out ulong? oldVersion)
            {
                Debug.Assert(newVersion is not 0);
                Debug.Assert(value is not null);
                oldVersion = UpsertDot(actorId, newVersion);
                
                var myTimestamp = Value.GetLastKnownTimestamp();
                var dtos = value.EnumerateDeltaDtos(myTimestamp).ToArray();
                Value.Merge(dtos);
                return dtos;
            }
            

            private ulong? UpsertDot(TActorId actorId, ulong newVersion)
            {
                var k = -1;
                for (var i = 0; i < Dots.Count; ++i)
                {
                    if (!Dots[i].Actor.Equals(actorId)) continue;

                    k = i;
                    break;
                }
            
                if (k >= 0)
                {
                    var oldVersion = Dots[k].Version;
                    Dots[k] = new Dot<TActorId>(actorId, newVersion);
                    return oldVersion;
                }
                
                Dots.Add(new Dot<TActorId>(actorId, newVersion));
                return default;
            }
        }
        
        public sealed record CausalTimestamp(IReadOnlyDictionary<TActorId, Range[]> Since, 
                                               IReadOnlyDictionary<TKey, TValueTimestamp> ValueTimestamps);

        
        public abstract record KeyDeltaDto(TActorId Actor)
        {
            public static KeyDeltaDto Added(TKey key, TActorId actor, ulong version) 
                => new KeyDeltaDtoAddition(key, actor, version);
            public static KeyDeltaDto Removed(TActorId actor, Range range) 
                => new KeyDeltaDtoDeletedRange(actor, range);
            public static KeyDeltaDto Removed(TActorId actor, ulong version) 
                => new KeyDeltaDtoDeletedDot(actor, version);
        }
        
        public sealed record KeyDeltaDtoAddition(TKey Key, TActorId Actor, ulong Version) : KeyDeltaDto(Actor);
        public sealed record KeyDeltaDtoDeletedDot(TActorId Actor, ulong Version) : KeyDeltaDto(Actor);
        public sealed record KeyDeltaDtoDeletedRange(TActorId Actor, Range Range) : KeyDeltaDto(Actor);
        
        
        public sealed record DeltaDto(
            KeyDeltaDto[] KeysDeltas,
            TValueDto[] ValueDeltas,
            TKey? Key
        );
    }
}
