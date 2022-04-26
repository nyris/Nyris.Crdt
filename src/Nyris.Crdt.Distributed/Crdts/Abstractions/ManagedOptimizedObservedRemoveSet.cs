using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Contracts.Exceptions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    /// <summary>
    /// Optimized Observed-Remove Set is a CRDT proposed by Annette Bieniusa & Co: https://softech.cs.uni-kl.de/homepage/staff/AnnetteBieniusa/paper/techrep2012-semantics.pdf
    /// It allows set of actors to add and remove elements unlimited number of times.
    /// Contrary to original Observed-Remove Set, it has an upper bound on memory usage.
    /// It is O(E*n + n), where E is the number of elements and n is the number of actors.
    /// </summary>
    [DebuggerDisplay("{_items.Count < 10 ? string.Join(';', _items) : \"... a lot of items ...\"}")]
    public abstract class ManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>
        : ManagedCRDT<TDto>
        where TItem : IEquatable<TItem>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
        where TDto : ManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>.OrSetDto, new()
    {
        private HashSet<VersionedSignedItem<TActorId, TItem>> _items;
        private readonly Dictionary<TActorId, uint> _observedState;
        private readonly SemaphoreSlim _semaphore = new(1);

        protected ManagedOptimizedObservedRemoveSet(InstanceId id,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
            _items = new HashSet<VersionedSignedItem<TActorId, TItem>>();
            _observedState = new Dictionary<TActorId, uint>();
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
                return HashingHelper.Combine(
                    HashingHelper.Combine(_items.OrderBy(i => i.Actor)),
                    HashingHelper.Combine(_observedState.OrderBy(pair => pair.Key)));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // NOTE: Logic Explained with Set Theory https://miro.com/app/board/uXjVObzJXTU=/
        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(TDto other, CancellationToken cancellationToken = default)
        {
            if (other.ObservedState is null || other.ObservedState.Count == 0) return MergeResult.NotUpdated;

            var otherItems = other.Items is null ? new HashSet<VersionedSignedItem<TActorId, TItem>>() : new HashSet<VersionedSignedItem<TActorId, TItem>>(other.Items);

            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }
            try
            {
                // NOTE: Keeps items from Other Node if Local Node has never seen those (i.e No ObservedState/VersionVector present) or they have newer Version than Local Observed State
                var newerOtherItems = otherItems.Where(i =>
                    !_observedState.TryGetValue(i.Actor, out var exitingVersion) || i.Version > exitingVersion).ToHashSet();
                // NOTE: If Other ObservedState has a Version that is higher than it's local Version, it means either It has new items or some item was deleted
                var anyDeleted =
                    other.ObservedState.Any(pair => _observedState.TryGetValue(pair.Key, out var localVersion) && pair.Value > localVersion);

                if (!newerOtherItems.Any() && !anyDeleted)
                {
                    return MergeResult.Identical;
                }

                // NOTE: These Local Items are Deleted when Other ObservedState has newer version but not the Item
                var deletedOrOlderItems = _items.Where(i =>
                    !otherItems.Contains(i) && other.ObservedState.TryGetValue(i.Actor, out var otherVersion) && i.Version < otherVersion).ToHashSet();

                // NOTE: Discard Deleted/Older Items
                _items.ExceptWith(deletedOrOlderItems);

                _items.UnionWith(newerOtherItems);

                // observed state is a element-wise max of two vectors.
                foreach (var (actorId, _) in _observedState.Union(other.ObservedState))
                {
                    _observedState.TryGetValue(actorId, out var thisObservedState);
                    other.ObservedState.TryGetValue(actorId, out var otherObservedState);

                    _observedState[actorId] = Math.Max(thisObservedState, otherObservedState);
                }
            }
            finally
            {
                _semaphore.Release();
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
                    Items = _items
                        .Select(i => new VersionedSignedItem<TActorId, TItem>(i.Actor, i.Version, i.Item))
                        .ToHashSet(),
                    ObservedState = _observedState
                        .ToDictionary(pair => pair.Key, pair => pair.Value)
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await ToDtoAsync(cancellationToken); // unfortunately making ORSet a delta Crdt is not an easy task
        }

        public HashSet<TItem> Value
        {
            get
            {
                if (!_semaphore.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new NyrisException("Deadlock");
                }
                try { return _items.Select(i => i.Item).ToHashSet(); }
                finally { _semaphore.Release(); }
            }
        }

        public virtual async Task AddAsync(TItem item, TActorId actorPerformingAddition)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }
            try
            {
                // default value for int is 0, so if key is not preset, lastObservedVersion will be assigned 0, which is intended
                _observedState.TryGetValue(actorPerformingAddition, out var observedVersion);
                ++observedVersion;

                _items.Add(new VersionedSignedItem<TActorId, TItem>(actorPerformingAddition, observedVersion, item));

                // notice that i.Actor.Equals(actorPerformingAddition) means that there may be multiple copies of item
                // stored at the same time. This is by design
                _items.RemoveWhere(i =>
                    i.Item.Equals(item) && i.Version < observedVersion && i.Actor.Equals(actorPerformingAddition));
                _observedState[actorPerformingAddition] = observedVersion;
            }
            finally
            {
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
                _items.RemoveWhere(i => condition(i.Item));
            }
            finally
            {
                _semaphore.Release();
            }
            await StateChangedAsync();
        }

        public abstract class OrSetDto
        {
            public abstract HashSet<VersionedSignedItem<TActorId, TItem>>? Items { get; set; }
            public abstract Dictionary<TActorId, uint>? ObservedState { get; set; }
        }
    }
}