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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Extensions;
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
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly ConcurrentDictionary<TKey, DottedValue> _items = new();
        private readonly ConcurrentDictionary<TActorId, MapVersionContext<TKey>> _context = new();

        private readonly ILogger<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>> _logger;
        public string Id = Guid.NewGuid().ToString()[..8];

        public ObservedRemoveMap(ILogger<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>> logger)
        {
            _logger = logger;
        }

        public int Count => _items.Count;
        public ICollection<TKey> Keys => _items.Keys;


        public DeltaDto AddOrMerge(TActorId actorId, TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                var context = GetOrCreateContext(actorId);
                var newVersion = context.GetNewVersion(key);
                _logger.LogDebug("{Id} new dot ({Actor}, {Version}) added for key {Key} (addition)",
                                 Id, actorId.ToString()![..8], newVersion, key);

                // if key already exists, we merge provided value with existing one and update dots
                if (_items.TryGetValue(key, out var dottedValue))
                {
                    // merge value, add new version to value's dot list and maybe remove old version (if there is one for this actor)
                    var valueDeltas = dottedValue.MergeValueAndUpsertDot(actorId, value, newVersion, out var oldVersion);
                    _logger.LogDebug("{Id} new value for key {Key} is merged with pre-existing value", Id, key);

                    // generate deltas for added/removed dots
                    var keysDeltas = GetKeyDeltas(actorId, key, newVersion, oldVersion);

                    // if old version was removed, we can drop it from inverse map
                    context.MaybeClearVersion(oldVersion);
                    if (oldVersion.HasValue)
                    {
                        _logger.LogDebug("{Id} old dot ({Actor}, {Version}) for key {Key} was cleared (addition)",
                                         Id, actorId.ToString()![..8], oldVersion.Value, key);
                    }

                    return new DeltaDto(keysDeltas, valueDeltas, key);
                }
                else // if the key is new one, simply add the value and get all it's dtos into one array
                {
                    _items[key] = new DottedValue(value, new Dot<TActorId>(actorId, newVersion));
                    _logger.LogDebug("{Id} key {Key} is new for this map, value added with dot ({Actor}, {Version})",
                                     Id, key, actorId.ToString()![..8], newVersion);
                    var keysDeltas = GetKeyDeltas(actorId, key, newVersion);
                    var valueDtos = value.EnumerateDeltaDtos().ToImmutableArray();
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
                    _logger.LogDebug("{Id} can not remove key {Key}, as it's not in _items", Id, key);
                    delta = default;
                    return false;
                }

                _logger.LogDebug("{Id} dotted value for {Key} removed from _items", Id, key);

                var dots = dottedValue.Dots;

                var keysDelta = ImmutableArray.CreateBuilder<KeyDeltaDto>(dots.Count);
                foreach (var (actor, version) in dots)
                {
                    keysDelta.Add(KeyDeltaDto.Removed(actor, version));
                    _context[actor].ClearVersion(version);
                    _logger.LogDebug("{Id} ({Actor}, {Version}) dot removed", Id, actor, version);
                }

                delta = new DeltaDto(keysDelta.MoveToImmutable(), ImmutableArray<TValueDto>.Empty, key);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }


        public bool TryMutate(
            TActorId actorId,
            TKey key,
            Func<TValue, ImmutableArray<TValueDto>> func,
            [NotNullWhen(true)] out DeltaDto? delta
        )
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_items.TryGetValue(key, out var dottedValue))
                {
                    _logger.LogDebug("{Id} can't mutate {Key}, as it's not in _items", Id, key);
                    delta = default;
                    return false;
                }

                var context = GetOrCreateContext(actorId);
                var newVersion = context.GetNewVersion(key);
                _logger.LogDebug("{Id} new dot ({Actor}, {Version}) added for key {Key} (mutation)",
                                 Id, actorId.ToString()![..8], newVersion, key);

                var valueDeltas = dottedValue.MutateValueAndUpsertDot(actorId, func, newVersion, out var oldVersion);
                _logger.LogDebug("{Id} mutation of key {Key} produced value deltas: {Deltas}", Id, key,
                                 JsonConvert.SerializeObject(valueDeltas));

                var keysDeltas = GetKeyDeltas(actorId, key, newVersion, oldVersion);
                context.MaybeClearVersion(oldVersion);
                if (oldVersion.HasValue)
                {
                    _logger.LogDebug("{Id} old dot ({Actor}, {Version}) for key {Key} was cleared (mutation)",
                                     Id, actorId.ToString()![..8], oldVersion.Value, key);
                }

                delta = new DeltaDto(keysDeltas, valueDeltas, key);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // TODO: updates lead to lost deltas - how to deal with that?
        public bool TryGet(TKey key, out TValue? value)
        {
            if (!_items.TryGetValue(key, out var dottedValue))
            {
                value = default;
                return false;
            }

            value = dottedValue.Value;
            return true;
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
            var since = _context
                .ToImmutableDictionary(pair => pair.Key, pair => pair.Value.GetRanges());
            // return _items.Count < 10000
            //            ? new CausalTimestamp(since, _items.ToDictionary(pair => pair.Key, pair => pair.Value.Value.GetLastKnownTimestamp()))
            // : new CausalTimestamp(since, ImmutableDictionary<TKey, TValueTimestamp>.Empty);
            return new CausalTimestamp(since, ImmutableDictionary<TKey, TValueTimestamp>.Empty);
        }

        public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? timestamp = default)
        {
            var since = timestamp?.Since ?? ImmutableDictionary<TActorId, ImmutableArray<Range>>.Empty;
            // var valueTimestamps = timestamp?.ValueTimestamps ?? ImmutableDictionary<TKey, TValueTimestamp>.Empty;

            var deltas = new List<DeltaDto>();

            _lock.EnterReadLock();
            try
            {
                // 'remove' deltas - keys that were removed. Include all known removals, as we do not pass info on what is known about removals
                foreach (var (actor, context) in _context)
                {
                    var emptyRanges = context.GetEmptyRanges();
                    // TODO: batch those? Is there a scenario when a number of empty ranges is too large?
                    var keyDeltas = GetKeyDeltas(actor, emptyRanges);

                    if (keyDeltas.Length > 0)
                    {
                        var dto = new DeltaDto(keyDeltas, ImmutableArray<TValueDto>.Empty, default, false);
                        // var line = string.Join(", ", keyDeltas.Select(kd =>
                        // {
                        //     var actorId = kd.Actor.ToString()![..8];
                        //     var range = ((KeyDeltaDtoDeletedRange) kd).Range;
                        //     return $"{actorId}: {range}";
                        // }));
                        // _logger.LogDebug("{Id} Yielding removed ranges: {Line}", Id, line);
                        
                        // yield return dto;
                        deltas.Add(dto);
                    }
                }

                // 'add' deltas - keys and values that were added/updated after provided timestamp.
                var seenKeys = new HashSet<TKey>(_items.Count);
                foreach (var (actor, context) in _context)
                {
                    if (!since.TryGetValue(actor, out var ranges)) ranges = ImmutableArray<Range>.Empty;
                    foreach (var key in context.EnumerateKeysOutsideRanges(ranges))
                    {
                        if (!seenKeys.Add(key) || !_items.TryGetValue(key, out var dottedValue)) continue;
                        // valueTimestamps.TryGetValue(key, out var valueTimestamp);

                        // TODO: value dtos definitely may be too large, need to batch them
                        var dto = dottedValue.ToDto(key, since);

                        _logger.LogDebug("{Id} Yielding dtos for key {Key}: {Json}", Id, key, JsonConvert.SerializeObject(dto));
                        // yield return dto;
                        deltas.Add(dto);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return deltas;
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
            var (keysDeltas, valueDeltas, key, knownKey) = delta;
            if (knownKey)
            {
                var dottedValue = _items.GetOrAdd(key!, _ => new DottedValue(new TValue()));

                var mergeValue = false;
                foreach (var deltaDto in keysDeltas)
                {
                    mergeValue |= MergeKeyDot(deltaDto, dottedValue);
                }

                // if after merging key dots we did not see anything new, ignore the value deltas as well - otherwise we might see old data
                if (mergeValue && !valueDeltas.IsEmpty)
                {
                    _logger.LogDebug("{Id} merging value dtos for key {Key}: {Dtos}", 
                                     Id, key, JsonConvert.SerializeObject(valueDeltas));
                    dottedValue.Value.Merge(valueDeltas);
                }
                
                // if after all merges dottedValue contains zero dots, then it can be removed
                if (dottedValue.Dots.Count > 0) return;
                
                _logger.LogDebug("{Id} dropping key {Key} as after merging key dots there are no more dots left", Id, key);
                _items.TryRemove(key!, out _);
            }
            else // delta covers unknown number of keys - those only happen when we pass removed ranges
            {
                // at this point we know the specific type of deltas. 
                // Use Unsafe.As simply for an allocation-free cast 
                var allDeltas = delta.KeysDeltas;
                Debug.Assert(allDeltas.All(d => d is KeyDeltaDtoDeletedRange));
                var deltas = Unsafe.As<ImmutableArray<KeyDeltaDto>, ImmutableArray<KeyDeltaDtoDeletedRange>>(ref allDeltas);
                
                foreach (var (actorId, range) in deltas)
                {
                    var context = GetOrCreateContext(actorId);
                    // remove dots from 'inverse' and get a list of keys that _may_ be deleted 
                    var candidateKeysToDrop = context.ObserveAndClear(range);
                    // _logger.LogDebug("{Id} for actor {Actor} range {Range} was observed and cleared", 
                    //                  Id, actorId.ToString()![..8], range);
                    foreach (var candidateKey in candidateKeysToDrop)
                    {
                        // remove the dot from a respective dottedValue. If there are no more dots after this, key-value
                        // pair can be dropped entirely
                        if (_items.TryGetValue(candidateKey, out var dottedValue) 
                            && dottedValue.ClearRangeAndCheckIsEmpty(actorId, range))
                        {
                            _logger.LogDebug("{Id} for actor {Actor} range {Range} caused key {Key} to be dropped", 
                                             Id, actorId.ToString()![..8], range, candidateKey);
                            _items.TryRemove(candidateKey, out _);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Merge key delta dto into the dotted value and context. 
        /// </summary>
        /// <param name="delta"></param>
        /// <param name="dottedValue"></param>
        /// <returns>True if new dot was added, false otherwise.</returns>
        /// <exception cref="AssumptionsViolatedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private bool MergeKeyDot(KeyDeltaDto delta, DottedValue dottedValue)
        {
            var dots = dottedValue.Dots;
            var context = GetOrCreateContext(delta.Actor);
            switch (delta)
            {
                case KeyDeltaDtoAddition(var key, var actorId, var otherVersion):
                    // try to insert into context. False means we already observed this dot, so return immediately
                    if (!context.TryInsert(key, otherVersion)) return false;
                    
                    // check if this actor is already present in dot list. If saved version for it is lower than delta's, update the dot
                    for (var i = 0; i < dots.Count; ++i)
                    {
                        var (actor, myVersion) = dots[i];
                        if (!actor.Equals(actorId)) continue;
                        if (myVersion >= otherVersion)
                        {
                            // this means, locally we have a newer update from this actor. Clear previously added dot
                            context.ClearVersion(otherVersion);
                            return true;
                        }

                        _logger.LogDebug("{Id} overwriting my dot ({Actor}, {Version}) for key {Key} with ({Actor}, {OtherVersion})", 
                                         Id, actor.ToString()![..8], myVersion, key, actor.ToString()![..8], otherVersion);
                        // clear myVersion, as version just inserted is higher and points to the same key
                        context.ClearVersion(myVersion);
                        dots[i] = new Dot<TActorId>(actorId, otherVersion);
                        return true;
                    }
                    
                    _logger.LogDebug("{Id} for key {Key} adding new dot ({Actor}, {Version})", 
                                     Id, key, actorId.ToString()![..8], otherVersion);
                    dots.Add(new Dot<TActorId>(actorId, otherVersion));
                    return true;
                case KeyDeltaDtoDeletedDot(var actorId, var otherVersion):
                    // check if this actor is already present in dot list. If saved version is not greater than delta's, remove the dot
                    for (var i = 0; i < dots.Count; ++i)
                    {
                        var (actor, myVersion) = dots[i];
                        if (!actor.Equals(actorId)) continue;
                        if (myVersion > otherVersion) break; // old removal, we know that value was added again later
                        
                        _logger.LogDebug("{Id} dot ({Actor}, {Version}) is removed as incoming empty dot has greater version {OtherVersion}", 
                                         Id, actor.ToString()![..8], myVersion, otherVersion);
                        context.ClearVersion(myVersion);
                        dots.RemoveAt(i);
                        break;
                    }
                    // finally make sure context have record of this dot
                    
                    _logger.LogDebug("{Id} dot ({Actor}, {Version}) observed to be empty", 
                                     Id, delta.Actor.ToString()![..8], otherVersion);
                    context.MergeVersion(otherVersion);
                    break;
                case KeyDeltaDtoDeletedRange:
                    throw new AssumptionsViolatedException("Delta for removing range of dots can not be applied to a single dottedValue");
                default:
                    throw new ArgumentOutOfRangeException(nameof(delta));
            }

            return false;
        }

        private MapVersionContext<TKey> GetOrCreateContext(TActorId actorId)
            => _context.GetOrAdd(actorId, _ => new MapVersionContext<TKey>()); 

        private static ImmutableArray<KeyDeltaDto> GetKeyDeltas(TActorId actor, ImmutableArray<Range> emptyRanges)
        {
            var keyDeltas = new KeyDeltaDto[emptyRanges.Length];
            for (var i = 0; i < emptyRanges.Length; ++i)
            {
                keyDeltas[i] = KeyDeltaDto.Removed(actor, emptyRanges[i]);
            }

            return Unsafe.As<KeyDeltaDto[], ImmutableArray<KeyDeltaDto>>(ref keyDeltas);
        }
        
        private static ImmutableArray<KeyDeltaDto> GetKeyDeltas(
            TActorId actorId,
            TKey key,
            ulong version) => ImmutableArray.Create(KeyDeltaDto.Added(key, actorId, version));
        
        private static ImmutableArray<KeyDeltaDto> GetKeyDeltas(
            TActorId actorId,
            TKey key,
            ulong newVersion,
            ulong? oldVersion) 
            => oldVersion.HasValue
                   ? ImmutableArray.Create(KeyDeltaDto.Added(key, actorId, newVersion),
                                           KeyDeltaDto.Removed(actorId, oldVersion.Value))
                   : ImmutableArray.Create(KeyDeltaDto.Added(key, actorId, newVersion));


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
            public DeltaDto ToDto(TKey key, ImmutableDictionary<TActorId, ImmutableArray<Range>> except)
            {
                var keyDeltas = ImmutableArray.CreateBuilder<KeyDeltaDto>(Dots.Count);
                foreach (var (dotActor, version) in Dots)
                {
                    if (!except.TryGetValue(dotActor, out var ranges) ||
                        ranges.Contains(version))
                    {
                        keyDeltas.Add(KeyDeltaDto.Added(key, dotActor, version));
                    }
                }
                
                var valueTimestamps = Value.EnumerateDeltaDtos().ToImmutableArray();
                return new DeltaDto(keyDeltas.ToImmutable(), valueTimestamps, key);
            }

            public bool ClearRangeAndCheckIsEmpty(TActorId actorId, Range range)
            {
                var i = FindDot(actorId);
                if (i < 0) return Dots.Count == 0;

                var version = Dots[i].Version;
                if (version >= range.From && version < range.To)
                {
                    Dots.RemoveAt(i);
                }

                return Dots.Count == 0;
            }
            
            public ImmutableArray<TValueDto> MutateValueAndUpsertDot(
                TActorId actorId,
                Func<TValue, ImmutableArray<TValueDto>> func,
                ulong newVersion,
                out ulong? oldVersion
            )
            {
                oldVersion = UpsertDot(actorId, newVersion);
                return func(Value);
            }
            
            public ImmutableArray<TValueDto> MergeValueAndUpsertDot(TActorId actorId, TValue value, ulong newVersion, out ulong? oldVersion)
            {
                Debug.Assert(newVersion is not 0);
                Debug.Assert(value is not null);
                oldVersion = UpsertDot(actorId, newVersion);
                
                var myTimestamp = Value.GetLastKnownTimestamp();
                var dtos = value.EnumerateDeltaDtos(myTimestamp).ToImmutableArray();
                Value.Merge(dtos);
                return dtos;
            }

            private int FindDot(TActorId actorId)
            {
                for (var i = 0; i < Dots.Count; ++i)
                {
                    if (Dots[i].Actor.Equals(actorId)) return i;
                }

                return -1;
            }
            
            private ulong? UpsertDot(TActorId actorId, ulong newVersion)
            {
                var i = FindDot(actorId);
                if (i >= 0)
                {
                    var oldVersion = Dots[i].Version;
                    Dots[i] = new Dot<TActorId>(actorId, newVersion);
                    return oldVersion;
                }
                
                Dots.Add(new Dot<TActorId>(actorId, newVersion));
                return default;
            }
        }
        
        public sealed record CausalTimestamp(ImmutableDictionary<TActorId, ImmutableArray<Range>> Since, 
                                             ImmutableDictionary<TKey, TValueTimestamp> ValueTimestamps);

        
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
            ImmutableArray<KeyDeltaDto> KeysDeltas,
            ImmutableArray<TValueDto> ValueDeltas,
            TKey? Key,
            bool KnownKey = true
        )
        {
            public bool Equals(DeltaDto? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;

                return KeysDeltas.ToHashSet().SetEquals(other.KeysDeltas) 
                       && ValueDeltas.ToHashSet().SetEquals(other.ValueDeltas) 
                       && EqualityComparer<TKey?>.Default.Equals(Key, other.Key) 
                       && KnownKey == other.KnownKey;
            }

            public override int GetHashCode()
            {
                var keysHash = KeysDeltas.OrderBy(d => d.Actor).Aggregate(0, HashCode.Combine);
                var valuesHash = ValueDeltas.OrderBy(d => d!.GetHashCode()).Aggregate(0, HashCode.Combine);
                return HashCode.Combine(keysHash, valuesHash, Key, KnownKey);
            }
        }
    }
}
