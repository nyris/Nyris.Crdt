using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Nyris.Crdt.Extensions;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Model;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Sets;

public class OptimizedObservedRemoveSetV3<TActorId, TItem> : OptimizedObservedRemoveCore<TActorId, TItem> 
    where TItem : IEquatable<TItem>
    where TActorId : IEquatable<TActorId>
{
    private readonly ConcurrentDictionary<TItem, List<(TActorId Actor, ulong Version)>> _items = new();

    public ImmutableArray<DeltaDto> Add(TItem item, TActorId actorId)
    {
        var version = GetNewVersion(actorId);
        AddDot(actorId, version, item);
        AddToContextAndInverse(actorId, version, item);
        return ImmutableArray.Create(DeltaDto.Added(item, actorId, version));
    }

    public ImmutableArray<DeltaDto> Remove(TItem item)
    {
        if (_items.TryRemove(item, out var dots))
        {
            var builder = ImmutableArray.CreateBuilder<DeltaDto>(dots.Count);
            foreach (var (actor, version) in dots)
            {
                RemoveFromInverse(actor, version);
                builder.Add(DeltaDto.Removed(actor, version));
            }
                
            // remove callbacks
            return builder.MoveToImmutable();
        }
        return ImmutableArray<DeltaDto>.Empty;
    }

    protected sealed override ulong? AddDot(TActorId actorId, ulong version, TItem item)
    {
        var dots = _items.GetOrAdd(item, _ => new List<(TActorId Actor, ulong Version)>(1));
        lock (dots)
        {
            if (dots.Count == 0)
            {
                // add callbacks
            }
                
            var i = GetDotIndex(dots, actorId);

            if (i < 0)
            {
                dots.Add((actorId, version));
                return null;
            }

            var oldVersion = dots[i].Version;
            if (oldVersion > version) return null;
                    
            dots[i] = (actorId, version);
            return oldVersion;
        }
    }

    protected sealed override void RemoveDot(TActorId actorId, ulong version, TItem item)
    {
        if (!_items.TryGetValue(item, out var dots)) return;

        lock (dots)
        {
            var i = GetDotIndex(dots, actorId);
            if (i < 0) return;
                
            dots.RemoveAt(i);

            if (dots.Count == 0)
            {
                _items.TryRemove(item, out _);
                // remove callbacks
            }
        }
    }

    private static int GetDotIndex(List<(TActorId Actor, ulong Version)> dots, TActorId actorId)
    {
        var index = -1;
        for (var i = 0; i < dots.Count; ++i)
        {
            if (!dots[i].Actor.Equals(actorId)) continue;

            index = i;
            break;
        }

        return index;
    }
}

/// <summary>
/// This is a base class for collections using Actored ObservedRemove mechanics.
/// You should not use this class directly unless you know what are you doing. 
/// </summary>
/// <typeparam name="TActorId"></typeparam>
/// <typeparam name="TDeltaItem"></typeparam>
public abstract class OptimizedObservedRemoveCore<TActorId, TDeltaItem> 
    : IDeltaCrdt<OptimizedObservedRemoveCore<TActorId, TDeltaItem>.DeltaDto, OptimizedObservedRemoveCore<TActorId, TDeltaItem>.CausalTimestamp>
    where TActorId : IEquatable<TActorId>
{
    private readonly Dictionary<TActorId, ulong> _localActorCounters = new(1);
        
    // To partially avoid locking, I enforce the order of operations. For _adding_ new data
    // into context and inverse, I always first add into inverse, than into context. 
    // Imagine if we were to add to context first, then to inverse.  
    // If there was a delta enumeration happening at the time, it could produce a RemovedDelta
    // (if it read version from context but did not see version in inverse yet)
    // And as Remove always overwrites corresponding addition for a given dot, we would lost this addition. 
    // To avoid this, we firs add versions to inverse, then to context.
    // Note that this is only relevant for additions - removals can be done in any order, as they have a priority 
    private readonly ConcurrentDictionary<TActorId, ConcurrentVersionRanges> _context = new();
    private readonly ConcurrentDictionary<TActorId, ConcurrentSkipListMap<ulong, TDeltaItem>> _inverse = new();
        
    // All above considerations are only valid for enumeration. We still need locking for Merging,
    // as there are branching decisions about what to do based on success or failure of updating context 
    private readonly object _lock = new();

    public CausalTimestamp GetLastKnownTimestamp() => 
        new(_context.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutable()));

    public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? timestamp = default)
    {
        var since = timestamp?.Since ?? ImmutableDictionary<TActorId, ImmutableArray<Range>>.Empty;

        foreach (var (actor, inverse) in _inverse)
        {
            if(!since.TryGetValue(actor, out var ignoreRanges)) ignoreRanges = ImmutableArray<Range>.Empty;
                
            // enumerate all existing versions that are NOT in 'since' - i.e. new items for the one who provided 'timestamp'
            foreach (var range in ignoreRanges.Inverse())
            {
                foreach (var (version, item) in inverse.WithinRange(range.From, range.To))
                {
                    yield return DeltaDto.Added(item, actor, version);
                }    
            }
                
            // enumerate all local gaps within 'since' - i.e. deleted dots (might or might not be new for the one providing timestamp)
            // careful to only take gaps that are actually within known ranges - i.e. gaps that intersect with context
            var knownRanges = _context.GetOrAdd(actor, _ => new ConcurrentVersionRanges());
            foreach (var range in knownRanges.IntersectWith(ignoreRanges))
            {
                var from = range.From;
                foreach (var (version, _) in inverse.WithinRange(range.From, range.To))
                {
                    if (version > from)
                    {
                        yield return DeltaDto.Removed(actor, new Range(from, version));
                        from = version + 1;
                    }
                }
                if(from < range.To) yield return DeltaDto.Removed(actor, new Range(from, range.To));
            }
        }
    }

    public DeltaMergeResult Merge(DeltaDto delta)
    {
        lock(_lock)
        {
            return MergeInternal(delta);
        }
    }
        
    public DeltaMergeResult Merge(ImmutableArray<DeltaDto> deltas)
    {
        lock(_lock)
        {
            var result = DeltaMergeResult.StateNotChanged;
            foreach (var delta in deltas)
            {
                if (MergeInternal(delta) == DeltaMergeResult.StateUpdated) result = DeltaMergeResult.StateUpdated;
            }

            return result;
        }
    }
        
    private DeltaMergeResult MergeInternal(DeltaDto delta)
    {
        var actorId = delta.Actor;
        var context = _context.GetOrAdd(actorId, _ => new ConcurrentVersionRanges());
        var inverse = _inverse.GetOrAdd(actorId, _ => new ConcurrentSkipListMap<ulong, TDeltaItem>());
            
        switch (delta)
        {
            case DeltaDtoAddition (var item, _, var version):
            {
                // if context already observed this dot - do nothing
                if (context.Contains(version)) return DeltaMergeResult.StateNotChanged; 
                    
                // It is possible to call ranges.Merge() inside if condition, thus avoiding calling "Contains"
                // However, this means that we break the order of insertions - first 'inverse', then 'context'
                inverse.TryAdd(version, item);
                context.Merge(version);
                    
                // How item is added it implementation dependent.
                var oldVersion = AddDot(actorId, version, item);
                if (oldVersion.HasValue) inverse.TryRemove(oldVersion.Value, out _);

                return DeltaMergeResult.StateUpdated;
            }
            case DeltaDtoDeletedDot (_, var version):
            {
                // Try to insert into context. If succeeded - we have not seen this dot and there is no need to try and check inverse. 
                if (context.Merge(version)) return DeltaMergeResult.StateUpdated;

                if (inverse.TryRemove(version, out var removedValue))
                {
                    RemoveDot(actorId, version, removedValue);
                    return DeltaMergeResult.StateUpdated;
                }
                    
                return DeltaMergeResult.StateNotChanged;
            }
            case DeltaDtoDeletedRange (_, var rangeToRemove):
            {
                // here we can't short-circuit after context update, as its a range  
                var contextUpdated = context.Merge(rangeToRemove);
                var inverseUpdated = false;
                foreach (var (version, item) in inverse.WithinRange(rangeToRemove.From, rangeToRemove.To))
                {
                    inverseUpdated = true;
                    inverse.TryRemove(version, out _);
                    RemoveDot(actorId, version, item);
                }

                return contextUpdated || inverseUpdated ? DeltaMergeResult.StateUpdated : DeltaMergeResult.StateNotChanged;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(delta));
        }
    }

    protected abstract ulong? AddDot(TActorId actorId, ulong version, TDeltaItem item);
    protected abstract void RemoveDot(TActorId actorId, ulong version, TDeltaItem item);
        
    protected void AddToContextAndInverse(TActorId actorId, ulong version, TDeltaItem item)
    {
        var context = _context.GetOrAdd(actorId, _ => new ConcurrentVersionRanges());
        var inverse = _inverse.GetOrAdd(actorId, _ => new ConcurrentSkipListMap<ulong, TDeltaItem>());

        inverse.TryAdd(version, item);
        context.Merge(version);
    }

    protected void RemoveFromInverse(TActorId actorId, ulong version)
    {
        var inverse = _inverse.GetOrAdd(actorId, _ => new ConcurrentSkipListMap<ulong, TDeltaItem>());
        inverse.TryRemove(version, out _);
    }
        
    /// <summary>
    /// Gets new version for a given actor that can be used to mutate state. Note that this does NOT update the context
    /// and <see cref="AddToContextAndInverse"/> method must be called  
    /// </summary>
    /// <param name="actorId"></param>
    /// <returns></returns>
    protected ulong GetNewVersion(TActorId actorId)
    {
        lock (_localActorCounters)
        {
            _localActorCounters.TryGetValue(actorId, out var version); // version defaults to 0 if actor not found
            return _localActorCounters[actorId] = version + 1; 
        }
    }
        
        
    public sealed record CausalTimestamp(ImmutableDictionary<TActorId, ImmutableArray<Range>> Since);
        
    public abstract record DeltaDto(TActorId Actor)
    {
        public static DeltaDto Added(TDeltaItem item, TActorId actor, ulong version) 
            => new DeltaDtoAddition(item, actor, version);
        public static DeltaDto Removed(TActorId actor, Range range) 
            => new DeltaDtoDeletedRange(actor, range);
        public static DeltaDto Removed(TActorId actor, ulong version) 
            => new DeltaDtoDeletedDot(actor, version);
    }

    public sealed record DeltaDtoAddition(TDeltaItem Item, TActorId Actor, ulong Version) : DeltaDto(Actor);
        
    public sealed record DeltaDtoDeletedDot(TActorId Actor, ulong Version) : DeltaDto(Actor);
        
    public sealed record DeltaDtoDeletedRange(TActorId Actor, Range Range) : DeltaDto(Actor);
}