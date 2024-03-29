using Microsoft.Extensions.Logging;
using Nyris.Contracts.Exceptions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Metrics;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions;

/// <summary>
/// Optimized Observed-Remove Set is a CRDT proposed by Annette Bieniusa & Co: https://softech.cs.uni-kl.de/homepage/staff/AnnetteBieniusa/paper/techrep2012-semantics.pdf
/// It allows set of actors to add and remove elements unlimited number of times.
/// Contrary to original Observed-Remove Set, it has an upper bound on memory usage.
/// It is O(E*n + n), where E is the number of elements and n is the number of actors.
/// </summary>
[DebuggerDisplay("{_items.Count < 10 ? string.Join(';', _items) : \"... a lot of items ...\"}")]
public abstract class ManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>
    : ManagedCRDT<TDto>, IDisposable
    where TItem : IEquatable<TItem>, IHashable
    where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
    where TDto : ManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>.OrSetDto, new()
{
    private readonly HashSet<DottedItemWithActor<TActorId, TItem>> _items;
    private TDto _delta;

    private readonly Dictionary<TActorId, uint> _versionVectors;

    // NOTE: Original Tombstones term represents the whole deleted Item {TItem} in original ORSet,
    // but this field only contains {Dot}s of deleted Items, this is also referred as "DotCloud" in https://bartoszsypytkowski.com/optimizing-state-based-crdts-part-2/
    private readonly Dictionary<IntDot<TActorId>, HashSet<TActorId>> _tombstones;
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly TActorId _thisNodeId;
    private readonly ICrdtMetricsRegistry? _metricsRegistry;

    private readonly TDto _defaultDelta;

    protected ManagedOptimizedObservedRemoveSet(
        InstanceId id,
        TActorId thisNodeId,
        IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null,
        ICrdtMetricsRegistry? metricsRegistry = null
    ) : base(id, queueProvider: queueProvider, logger: logger)
    {
        _thisNodeId = thisNodeId;
        _metricsRegistry = metricsRegistry;
        _items = new HashSet<DottedItemWithActor<TActorId, TItem>>();
        _defaultDelta = new TDto
        {
            Items = new HashSet<DottedItemWithActor<TActorId, TItem>>(),
            Tombstones = new Dictionary<IntDot<TActorId>, HashSet<TActorId>>(),
            VersionVectors = new Dictionary<TActorId, uint>(),
            SourceId = thisNodeId
        };
        _delta = _defaultDelta;
        _versionVectors = new Dictionary<TActorId, uint>();
        _tombstones = new Dictionary<IntDot<TActorId>, HashSet<TActorId>>();
    }

    /// <inheritdoc />
    public override ReadOnlySpan<byte> CalculateHash()
    {
        if (!_semaphore.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            // TODO: Hash of set should suffice only with VersionVectors, content of Items should be irrelevant
            return HashingHelper.Combine(
                                         HashingHelper.Combine(_items.OrderBy(i => i.Dot.Actor)),
                                         HashingHelper.Combine(_versionVectors.OrderBy(pair => pair.Key)));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public override async Task<MergeResult> MergeAsync(TDto other, CancellationToken cancellationToken = default)
    {
        if ((other.SourceId is not null && _thisNodeId.Equals(other.SourceId)) ||
            (other.VersionVectors is null || other.VersionVectors.Count == 0)) return MergeResult.NotUpdated;

        var otherItems = other.Items is null
                             ? new HashSet<DottedItemWithActor<TActorId, TItem>>()
                             : new HashSet<DottedItemWithActor<TActorId, TItem>>(other.Items);

        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            _metricsRegistry?.RecordMergeTrigger(TypeName);
            // NOTE: Destroy current delta and create new one on every merge
            // in case of failure we can always recreate it (but will have to wait for some other operation to happen to trigger `Merge`)
            _delta = _defaultDelta;

            // NOTE: Keeps items from Other Node if Local Node has never seen those (i.e No VersionVector present) or they have newer Dot than Local Observed State
            var newerOtherItems = otherItems.Where(i =>
                                                       !(other.Tombstones is not null && other.Tombstones.ContainsKey(i.Dot)) &&
                                                       (!_versionVectors.TryGetValue(i.Dot.Actor, out var exitingVersion) ||
                                                        i.Dot.Version > exitingVersion))
                                            .ToHashSet();

            var newerTombstones = other.Tombstones ?? new Dictionary<IntDot<TActorId>, HashSet<TActorId>>();

            // NOTE: Delta is Identical when
            // 0. There are No new items
            // 1. There NO newer Tombstones, i.e No Items were deleted or No Items were deleted that this Node hasn't seen
            if (newerOtherItems.Count == 0 && newerTombstones.Count == 0)
            {
                return MergeResult.Identical;
            }

            if (newerTombstones.Count != 0)
            {
                // NOTE: Local Items are Deleted when it's Dot exists in other Tombstones
                var deletedItems = _items.Where(i => newerTombstones.TryGetValue(i.Dot, out _)).ToHashSet();

                _items.ExceptWith(deletedItems);
                newerOtherItems.ExceptWith(deletedItems);
            }

            _items.UnionWith(newerOtherItems);

            // observed state is a element-wise max of two vectors.
            foreach (var (actorId, otherVersion) in other.VersionVectors)
            {
                if (_versionVectors.TryGetValue(actorId, out var existingVersion))
                {
                    _versionVectors[actorId] = Math.Max(otherVersion, existingVersion);
                }
                else
                {
                    _versionVectors.Add(actorId, otherVersion);
                }
            }

            var allKnownNodes = _versionVectors.Select(pair => pair.Key).OrderBy(id => id).ToHashSet();

            foreach (var (dot, actorIds) in newerTombstones)
            {
                var observedByActors = _tombstones.GetValueOrDefault(dot, new HashSet<TActorId>());

                var tombstoneWasKnownByAll = allKnownNodes.SetEquals(actorIds.OrderBy(id => id));

                // NOTE: Delete Tombstones which every known Node has already seen
                if (tombstoneWasKnownByAll)
                {
                    _tombstones.Remove(dot);
                }
                else
                {
                    observedByActors.UnionWith(actorIds);
                    // NOTE: We have processed this tombstone, when we deleted items based on this
                    observedByActors.Add(_thisNodeId);
                    // NOTE: Add/Update with Updated info of Nodes who have observed the give tombstone
                    _tombstones[dot] = observedByActors;
                }
            }

            // TODO: Calculate deltas for each Node
            _delta.Items?.UnionWith(newerOtherItems);
            _delta.VersionVectors = _versionVectors;
            _delta.Tombstones = _tombstones;
        }
        finally
        {
            _semaphore.Release();

            _metricsRegistry?.CollectCollectionSize(_items.Count, _tombstones.Count, TypeName);
        }

        return MergeResult.ConflictSolved;
    }

    public override async Task<TDto> ToDtoAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            return new TDto
            {
                Items = _delta.Items?.ToHashSet(),
                VersionVectors = _delta.VersionVectors?.ToDictionary(pair => pair.Key, pair => pair.Value),
                Tombstones = _delta.Tombstones?.ToDictionary(pair => pair.Key, pair => pair.Value),
                SourceId = _thisNodeId
            };
        }
        finally
        {
            _metricsRegistry?.CollectDtoSize(_delta.Items?.Count, _delta.Tombstones?.Count, TypeName);
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return
            await ToDtoAsync(cancellationToken); // unfortunately making ORSet a delta Crdt is not an easy task
    }

    public IReadOnlyCollection<TItem> Value
    {
        get
        {
            if (!_semaphore.Wait(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                return _items.Select(i => i.Value).ToList().AsReadOnly();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public virtual async Task AddAsync(TItem item)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            _versionVectors.TryGetValue(_thisNodeId, out var version);

            version += 1;

            var newItem = new DottedItemWithActor<TActorId, TItem>(new IntDot<TActorId>(_thisNodeId, version), item);

            _items.Add(newItem);


            // notice that i.Actor.Equals(_thisNodeId) means that there may be multiple copies of item
            // stored at the same time. This is by design
            _items.RemoveWhere(i => i.Value.Equals(item) && i.Dot.Version < version);
            _versionVectors[_thisNodeId] = version;

            _delta.Items?.Add(newItem);
            _delta.VersionVectors = _versionVectors;
            _delta.Tombstones = _tombstones;
        }
        finally
        {
            _metricsRegistry?.CollectCollectionSize(_items.Count, _tombstones.Count, TypeName);

            _semaphore.Release();
        }

        await StateChangedAsync();
    }

    public virtual Task RemoveAsync(TItem item) => RemoveAsync(i => i.Equals(item));

    public virtual async Task RemoveAsync(Func<TItem, bool> condition)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            var itemsToBeRemoved = _items.Where(i => condition(i.Value)).ToHashSet();

            if (itemsToBeRemoved.Count > 0)
            {
                _items.ExceptWith(itemsToBeRemoved);

                foreach (var dottedItem in itemsToBeRemoved)
                {
                    if (_tombstones.TryGetValue(dottedItem.Dot, out var observedByActors))
                    {
                        observedByActors.Add(_thisNodeId);
                    }
                    else
                    {
                        _tombstones.Add(dottedItem.Dot, new HashSet<TActorId> { _thisNodeId });
                    }
                }

                _delta.Items?.ExceptWith(itemsToBeRemoved);
                _delta.VersionVectors = _versionVectors;
                _delta.Tombstones = _tombstones;
            }
        }
        finally
        {
            _metricsRegistry?.CollectCollectionSize(_items.Count, _tombstones.Count, TypeName);

            _semaphore.Release();
        }

        await StateChangedAsync();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _semaphore.Dispose();
    }

    public abstract class OrSetDto
    {
        public abstract HashSet<DottedItemWithActor<TActorId, TItem>>? Items { get; set; }
        public abstract Dictionary<TActorId, uint>? VersionVectors { get; set; }
        public abstract Dictionary<IntDot<TActorId>, HashSet<TActorId>>? Tombstones { get; set; }
        public abstract TActorId? SourceId { get; set; }
    }
}
