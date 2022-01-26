using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
    public abstract class ManagedOptimizedObservedRemoveSet<TImplementation, TActorId, TItem>
        : ManagedCRDT<
            TImplementation,
            HashSet<TItem>,
            ManagedOptimizedObservedRemoveSet<TImplementation, TActorId, TItem>.OrSetDto>
        where TItem : IEquatable<TItem>, IHashable
        where TActorId : IEquatable<TActorId>, IHashable
        where TImplementation : ManagedOptimizedObservedRemoveSet<TImplementation, TActorId, TItem>
    {
        private HashSet<VersionedSignedItem<TActorId, TItem>> _items;
        private readonly Dictionary<TActorId, uint> _observedState;
        private readonly SemaphoreSlim _semaphore = new(1);

        protected ManagedOptimizedObservedRemoveSet(string id) : base(id)
        {
            _items = new HashSet<VersionedSignedItem<TActorId, TItem>>();
            _observedState = new Dictionary<TActorId, uint>();
        }

        protected ManagedOptimizedObservedRemoveSet(OrSetDto orSetDto, string instanceId) : base(instanceId)
        {
            _items = orSetDto?.Items ?? new HashSet<VersionedSignedItem<TActorId, TItem>>();
            _observedState = orSetDto?.ObservedState ?? new Dictionary<TActorId, uint>();
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash()
        {
            _semaphore.Wait();
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

        public override async Task<OrSetDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
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

        public override HashSet<TItem> Value
        {
            get
            {
                _semaphore.Wait();
                try { return _items.Select(i => i.Item).ToHashSet(); }
                finally { _semaphore.Release(); }
            }
        }

        public virtual async Task AddAsync(TItem item, TActorId actorPerformingAddition)
        {
            await _semaphore.WaitAsync();
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
            await _semaphore.WaitAsync();
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

        public override async Task<MergeResult> MergeAsync(TImplementation other,
            CancellationToken cancellationToken = default)
        {
            if (CalculateHash().SequenceEqual(other.CalculateHash()))
            {
                return MergeResult.Identical;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // variables names a taken from the paper, they do not have obvious meaning by themselves
                var m = _items.Intersect(other._items);

                var m1 = _items
                    .Except(other._items)
                    .Where(i => !other._observedState.TryGetValue(i.Actor, out var otherVersion)
                                || i.Version > otherVersion);

                var m2 = other._items
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
                foreach (var actorId in _observedState.Keys.ToList().Union(other._observedState.Keys))
                {
                    _observedState.TryGetValue(actorId, out var thisVersion);
                    other._observedState.TryGetValue(actorId, out var otherVersion);
                    _observedState[actorId] = thisVersion > otherVersion ? thisVersion : otherVersion;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return MergeResult.ConflictSolved;
        }

        [ProtoContract]
        public sealed class OrSetDto
        {
            [ProtoMember(1)]
            public HashSet<VersionedSignedItem<TActorId, TItem>> Items { get; set; } = new();

            [ProtoMember(2)]
            public Dictionary<TActorId, uint> ObservedState { get; set; } = new();
        }
    }
}