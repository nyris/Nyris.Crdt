using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Extensions;
using Nyris.Crdt.Interfaces;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt;

[Obsolete("Please use ObservedRemoveMapV2 instead", false)]
public class ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>
    : IDeltaCrdt<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.DeltaDto,
        ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.CausalTimestamp>
    where TKey : IEquatable<TKey>
    where TValue : class, IDeltaCrdt<TValueDto, TValueTimestamp>, new()
    where TActorId : IEquatable<TActorId>
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ConcurrentDictionary<TKey, DottedValue> _items = new();
    private readonly ConcurrentDictionary<TActorId, MapVersionContext<TKey>> _context = new();

    private readonly ILogger<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>> _logger;

    public ObservedRemoveMap(ILogger<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>>? logger = null)
    {
        _logger = logger ?? NullLogger<ObservedRemoveMap<TActorId, TKey, TValue, TValueDto, TValueTimestamp>>.Instance;
    }

    public int Count => _items.Count;
    public ICollection<TKey> Keys => _items.Keys;


    public ImmutableArray<DeltaDto> AddOrMerge(TActorId actorId, TKey key, TValue value)
    {
        _lock.EnterWriteLock();
        try
        {
            var context = GetOrCreateContext(actorId);
            var newVersion = context.GetNewVersion(key);

            // if key already exists, we merge provided value with existing one and update dots
            if (_items.TryGetValue(key, out var dottedValue))
            {
                // merge value, add new version to value's dot list and maybe remove old version (if there is one for this actor)
                var valueDeltas = dottedValue.MergeValueAndUpdateDot(actorId, value, newVersion, out var oldVersion);
                if (!oldVersion.HasValue)
                {
                    return ImmutableArray.Create(DeltaDto.Added(actorId, newVersion, key, valueDeltas));
                }

                // if old version was removed, we can drop it from inverse map
                context.ClearVersion(oldVersion.Value);
                return ImmutableArray.Create(DeltaDto.Added(actorId, newVersion, key, valueDeltas),
                                             DeltaDto.Removed(actorId, oldVersion.Value));
            } 
            else // if the key is new one, simply add the value and get all it's dtos into one array
            {
                var valueDeltas = value.EnumerateDeltaDtos().ToImmutableArray();
                _items[key] = new DottedValue(value, new DotWithDeltas(actorId, newVersion, valueDeltas));
                return ImmutableArray.Create(DeltaDto.Added(actorId, newVersion, key, valueDeltas));
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryRemove(TKey key, [NotNullWhen(true)] out ImmutableArray<DeltaDto> deltas)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_items.TryRemove(key, out var dottedValue))
            {
                deltas = ImmutableArray<DeltaDto>.Empty;
                return false;
            }

            var dots = dottedValue.Dots;

            var deltasBuilder = ImmutableArray.CreateBuilder<DeltaDto>(dots.Count);
            foreach (var (actor, version, _) in dots)
            {
                deltasBuilder.Add(DeltaDto.Removed(actor, version));
                _context[actor].ClearVersion(version);
            }

            deltas = deltasBuilder.MoveToImmutable();
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
        out ImmutableArray<DeltaDto> delta
    )
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_items.TryGetValue(key, out var dottedValue))
            {
                delta = ImmutableArray<DeltaDto>.Empty;
                return false;
            }

            var context = GetOrCreateContext(actorId);
            var newVersion = context.GetNewVersion(key);

            var valueDeltas = dottedValue.MutateValueAndUpdateDot(actorId, func, newVersion, out var oldVersion);
            if (!oldVersion.HasValue)
            {
                delta = ImmutableArray.Create(DeltaDto.Added(actorId, newVersion, key, valueDeltas));
                return true;
            }
                
            context.ClearVersion(oldVersion.Value);
            delta = ImmutableArray.Create(DeltaDto.Added(actorId, newVersion, key, valueDeltas),
                                          DeltaDto.Removed(actorId, oldVersion.Value));
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
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
        => new(_context
                   .ToImmutableDictionary(pair => pair.Key, 
                                          pair => pair.Value.GetRanges()));

    public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? timestamp = default)
    {
        var since = timestamp?.Since ?? ImmutableDictionary<TActorId, ImmutableArray<Range>>.Empty;
        var deltas = new List<DeltaDto>();
            
        _lock.EnterReadLock();
        try
        {
            // 'remove' deltas - keys that were removed. Include all known removals, as we do not pass info on what is known about removals
            foreach (var (actor, context) in _context)
            {
                // TODO: there are 2 useless allocations of immutable arrays inside GetEmptyRanges
                var emptyRanges = context.GetEmptyRanges();
                foreach (var range in emptyRanges)
                {
                    deltas.Add(DeltaDto.Removed(actor, range));
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
                    deltas.AddRange(dottedValue.ToDtos(key, since));
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        return deltas;
    }

    public DeltaMergeResult Merge(DeltaDto delta)
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
        
    public DeltaMergeResult Merge(DeltaDto[] deltas)
    {
        _lock.EnterWriteLock();
        try
        {
            var stateUpdated = false;
            foreach (var delta in deltas)
            {
                stateUpdated |= DeltaMergeResult.StateUpdated == MergeInternal(delta);
            }

            return stateUpdated ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
        
    private DeltaMergeResult MergeInternal(DeltaDto delta)
    {
        var actorId = delta.Actor;
        var context = GetOrCreateContext(actorId);
        switch (delta)
        {
            case DeltaDtoAddition deltaDtoAddition:
                var (_, version, key, valueDeltas) = deltaDtoAddition;
                if (!context.TryInsert(key, version)) return DeltaMergeResult.StateNotChanged; // if context already observed this dot - do nothing
                    
                var dottedValue = _items.GetOrAdd(key, _ => new DottedValue(new TValue()));   // abstract AddDot(TItem item, TActorId a, ulong v, out long? oldVersion)
                dottedValue.Merge(actorId, version, valueDeltas, out var oldVersion);              // ^ 
                context.MaybeClearVersion(oldVersion);
                return DeltaMergeResult.StateUpdated;
            case DeltaDtoDeletedDot deltaDtoDeletedDot:
                version = deltaDtoDeletedDot.Version;
                // either dot was new, or there is a non-null key
                var removedKey = context.ObserveAndClear(version, out var newVersionWasInserted);   
                if (removedKey is null || removedKey.Equals(default))
                {
                    return newVersionWasInserted ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged; 
                } 

                dottedValue = _items.GetOrAdd(removedKey, _ => new DottedValue(new TValue()));   // abstract RemoveDot(TItem item, TActorId a, ulong v)  
                dottedValue.RemoveDot(actorId, version);
                return DeltaMergeResult.StateUpdated;
            case DeltaDtoDeletedRange deltaDtoDeletedRange: 
                var range = deltaDtoDeletedRange.Range;
                var candidateKeysToDrop = context.ObserveAndClear(range, out newVersionWasInserted);
                foreach (var (candidateKey, v) in candidateKeysToDrop)
                {
                    // remove the dot from a respective dottedValue. If there are no more dots after this, key-value
                    // pair can be dropped entirely
                    if (_items.TryGetValue(candidateKey, out dottedValue) 
                        && dottedValue.ClearRangeAndCheckIsEmpty(actorId, range))
                    {
                        _items.TryRemove(candidateKey, out _);
                    }
                }
                return (newVersionWasInserted || !candidateKeysToDrop.IsEmpty) ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged;
            default:
                throw new ArgumentOutOfRangeException(nameof(delta));
        }
    }

    private MapVersionContext<TKey> GetOrCreateContext(TActorId actorId)
        => _context.GetOrAdd(actorId, _ => new MapVersionContext<TKey>()); 

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
            
        public DottedValue(TValue value)
        {
            Value = value;
        }
        public DottedValue(TValue value, DotWithDeltas dot)
        {
            Value = value;
            Dots.Add(dot);
        }

        public List<DotWithDeltas> Dots { get; } = new(1);
        public TValue Value { get; private set; }

        [Pure]
        public List<DeltaDto> ToDtos(TKey key, ImmutableDictionary<TActorId, ImmutableArray<Range>> except)
        {
            var deltas = new List<DeltaDto>(Dots.Count);
            foreach (var (dotActor, version, valueDtos) in Dots)
            {
                if (!except.TryGetValue(dotActor, out var ranges) ||
                    ranges.Contains(version))
                {
                    deltas.Add(DeltaDto.Added(dotActor, version, key, valueDtos));
                }
            }
                
            return deltas;
        }

        public bool ClearRangeAndCheckIsEmpty(TActorId actorId, Range range)
        {
            var i = FindDot(actorId);
            if (i < 0) return Dots.Count == 0;

            var version = Dots[i].Version;
            if (version >= range.From && version < range.To)
            {
                RemoveDotAt(i);
            }

            return Dots.Count == 0;
        }
            
        public ImmutableArray<TValueDto> MutateValueAndUpdateDot(
            TActorId actorId,
            Func<TValue, ImmutableArray<TValueDto>> func,
            ulong newVersion,
            out ulong? oldVersion
        )
        {
            var deltas = func(Value);
            TryAddAndMaybeConcatDeltas(actorId, newVersion, deltas, true, out oldVersion);
            return deltas;
        }
            
        public ImmutableArray<TValueDto> MergeValueAndUpdateDot(TActorId actorId, TValue value, ulong newVersion, out ulong? oldVersion)
        {
            Debug.Assert(newVersion is not 0);
            Debug.Assert(value is not null);
                
            var myTimestamp = Value.GetLastKnownTimestamp();
            var deltas = value.EnumerateDeltaDtos(myTimestamp).ToImmutableArray();
                
            TryAddAndMaybeConcatDeltas(actorId, newVersion, deltas, true, out oldVersion);
                
            Value.Merge(deltas);
            return deltas;
        }

        public void Merge(TActorId actorId, ulong version, ImmutableArray<TValueDto> deltas, out ulong? oldVersion)
        {
            if (!TryAddOrUpdate(actorId, version, deltas, out oldVersion)) return;

            try
            {
                Value.Merge(deltas);
            }
            catch (AssumptionsViolatedException)
            {
                _conflictingDeltas = true;
            }
        }

        public void RemoveDot(TActorId actorId, ulong version)
        {
            var i = FindDot(actorId);
            if (i < 0 || Dots[i].Version != version) return;

            RemoveDotAt(i);
        }

        private void RemoveDotAt(int i)
        {
            var removed = Dots[i];
            Dots.RemoveAt(i);

            // try reverse the removed deltas.
            // allReversed will be true, when all TryReverse is true AND if conflicting deltas are set to false
            var allReversed = !_conflictingDeltas;
            foreach (var delta in removed.Deltas)
            {
                allReversed &= Value.TryReverse(delta);
            }

            if (allReversed) return;

            // if some deltas can't be reversed (or if there were conflicting deltas to begin with), we need to recreate the value from all other deltas
            try
            {
                var newValue = new TValue();
                foreach (var dot in Dots)
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
            for (var i = 0; i < Dots.Count; ++i)
            {
                if (Dots[i].Actor.Equals(actorId)) return i;
            }

            return -1;
        }
            
        private bool TryAddOrUpdate(TActorId actorId, ulong newVersion, ImmutableArray<TValueDto> deltas, out ulong? oldVersion)
        {
            var i = FindDot(actorId);
            if (i >= 0)
            {
                var savedVersion = Dots[i].Version;
                if (newVersion <= savedVersion)
                {
                    oldVersion = default;
                    return false;
                }

                    
                Dots.Add(new DotWithDeltas(actorId, newVersion, deltas));
                RemoveDotAt(i);
                oldVersion = savedVersion;
                return true;
            }
                
            Dots.Add(new DotWithDeltas(actorId, newVersion, deltas));
            oldVersion = default;
            return true;
        }
            
        private bool TryAddAndMaybeConcatDeltas(TActorId actorId,
                                                ulong newVersion,
                                                ImmutableArray<TValueDto> deltas,
                                                bool throwIfNotLatestDot,
                                                out ulong? oldVersion)
        {
            var i = FindDot(actorId);
            if (i >= 0)
            {
                var savedVersion = Dots[i].Version;
                if (newVersion <= savedVersion)
                {
                    if (throwIfNotLatestDot)
                    {
                        throw new AssumptionsViolatedException($"DottedValue contains dot ({actorId}, {savedVersion}), that" +
                                                               $"is greater then the new dot ({actorId}, {newVersion}).");
                    }

                    oldVersion = default;
                    return false;
                }
                    
                var oldDeltas = Dots[i].Deltas;
                var newDeltas = ImmutableArray.CreateBuilder<TValueDto>(oldDeltas.Length + deltas.Length);
                    
                foreach (var delta in oldDeltas) newDeltas.Add(delta);
                foreach (var delta in deltas) newDeltas.Add(delta);
                    
                Dots[i] = new DotWithDeltas(actorId, newVersion, newDeltas.MoveToImmutable());
                oldVersion = savedVersion;
                return true;
            }
                
            Dots.Add(new DotWithDeltas(actorId, newVersion, deltas));
            oldVersion = default;
            return true;
        }
    }
        
    public sealed record CausalTimestamp(ImmutableDictionary<TActorId, ImmutableArray<Range>> Since);

    public abstract record DeltaDto(TActorId Actor)
    {
        public static DeltaDto Added(TActorId actor,
                                     ulong version,
                                     TKey key,
                                     ImmutableArray<TValueDto> valueDtos) 
            => new DeltaDtoAddition(actor, version, key, valueDtos);
        public static DeltaDto Removed(TActorId actor, Range range) 
            => new DeltaDtoDeletedRange(actor, range);
        public static DeltaDto Removed(TActorId actor, ulong version) 
            => new DeltaDtoDeletedDot(actor, version);
    }
        
    public sealed record DeltaDtoAddition(
        TActorId Actor,
        ulong Version,
        TKey Key,
        ImmutableArray<TValueDto> ValueDtos) : DeltaDto(Actor);
    public sealed record DeltaDtoDeletedDot(TActorId Actor, ulong Version) : DeltaDto(Actor);
    public sealed record DeltaDtoDeletedRange(TActorId Actor, Range Range) : DeltaDto(Actor);
}