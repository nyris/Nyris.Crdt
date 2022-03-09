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
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    /// <summary>
    /// Optimized Observed-Remove Set is a CRDT proposed by Annette Bieniusa & Co: https://softech.cs.uni-kl.de/homepage/staff/AnnetteBieniusa/paper/techrep2012-semantics.pdf
    /// It allows set of actors to add and remove elements unlimited number of times.
    /// Contrary to original Observed-Remove Set, it has an upper bound on memory usage.
    /// It is O(E*n + n), where E is the number of elements and n is the number of actors.
    /// </summary>
    [DebuggerDisplay("{_items.Count < 10 ? string.Join(';', _items) : \"... a lot of items ...\"}")]
    public abstract class ManagedOptimizedObservedRemoveSet<TActorId, TItem>
        : ManagedCRDT<ManagedOptimizedObservedRemoveSet<TActorId, TItem>.OrSetDto>
        where TItem : IEquatable<TItem>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
    {
        private HashSet<VersionedSignedItem<TActorId, TItem>> _items;
        private readonly Dictionary<TActorId, uint> _observedState;
        private readonly SemaphoreSlim _semaphore = new(1);

        protected ManagedOptimizedObservedRemoveSet(string id,
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

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(OrSetDto other, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(other.ObservedState, null) || other.ObservedState.Count == 0) return MergeResult.NotUpdated;
            other.Items ??= new HashSet<VersionedSignedItem<TActorId, TItem>>();

            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }
            try
            {
                // variables names a taken from the paper, they do not have obvious meaning by themselves
                var m = _items.Intersect(other.Items).ToHashSet();

                // we need to check if received dto is identical to this instance in order to return correct merge result
                if (m.Count == _items.Count
                    && _items.OrderBy(item => item.Actor).SequenceEqual(other.Items.OrderBy(item => item.Actor))
                    && _observedState.OrderBy(pair => pair.Key).SequenceEqual(other.ObservedState.OrderBy(pair => pair.Key)))
                {
                    return MergeResult.Identical;
                }

                var m1 = _items
                    .Except(other.Items)
                    .Where(i => !other.ObservedState.TryGetValue(i.Actor, out var otherVersion)
                                || i.Version > otherVersion);

                var m2 = other.Items
                    .Except(_items)
                    .Where(i => !_observedState.TryGetValue(i.Actor, out var myVersion)
                                || i.Version > myVersion);

                var u = m.Union(m1).Union(m2);

                // TODO: maybe make it faster then O(n^2)?
                var o = _items
                    .Where(item => _items.Any(i => item.Item.Equals(i.Item)
                                                   && item.Actor.Equals(i.Actor)
                                                   && item.Version < i.Version));

                _items = u.Except(o).ToHashSet();

                // observed state is a element-wise max of two vectors.
                foreach (var actorId in _observedState.Keys.ToList().Union(other.ObservedState.Keys))
                {
                    _observedState.TryGetValue(actorId, out var thisVersion);
                    other.ObservedState.TryGetValue(actorId, out var otherVersion);
                    _observedState[actorId] = thisVersion > otherVersion ? thisVersion : otherVersion;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return MergeResult.ConflictSolved;
        }

        public override async Task<OrSetDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }
            try
            {
                return new OrSetDto
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
        public override async IAsyncEnumerable<OrSetDto> EnumerateDtoBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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

        [ProtoContract]
        public sealed class OrSetDto
        {
            [ProtoMember(1)]
            public HashSet<VersionedSignedItem<TActorId, TItem>>? Items { get; set; }

            [ProtoMember(2)]
            public Dictionary<TActorId, uint>? ObservedState { get; set; }
        }
    }
}