using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProtoBuf;

namespace Nyris.Crdt.Sets
{
    /// <summary>
    /// Optimized Observed-Remove Set is a CRDT proposed by Annette Bieniusa & Co: https://softech.cs.uni-kl.de/homepage/staff/AnnetteBieniusa/paper/techrep2012-semantics.pdf
    /// It allows set of actors to add and remove elements unlimited number of times.
    /// Contrary to original Observed-Remove Set, it has an upper bound on memory usage.
    /// It is O(E*n + n), where E is the number of elements and n is the number of actors.
    /// </summary>
    [DebuggerDisplay("{_items.Count < 10 ? string.Join(';', _items) : \"... a lot of items ...\"}")]
    public sealed class OptimizedObservedRemoveSet<TActorId, TItem>
        : ICRDT<
            OptimizedObservedRemoveSet<TActorId, TItem>,
            HashSet<TItem>,
            OptimizedObservedRemoveSet<TActorId, TItem>.Dto>
        where TItem : IEquatable<TItem>
        where TActorId : IEquatable<TActorId>
    {
        private HashSet<VersionedSignedItem<TActorId, TItem>> _items;
        private readonly Dictionary<TActorId, uint> _observedState;
        private readonly object _setChangeLock = new();

        public OptimizedObservedRemoveSet()
        {
            _items = new HashSet<VersionedSignedItem<TActorId, TItem>>();
            _observedState = new Dictionary<TActorId, uint>();
        }

        private OptimizedObservedRemoveSet(Dto dto)
        {
            _items = dto.Items;
            _observedState = dto.ObservedState;
        }

        public static OptimizedObservedRemoveSet<TActorId, TItem> FromDto(Dto dto)
            => new(dto);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            lock (_setChangeLock)
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                var itemsHash = _items
                    .OrderBy(i => i.Actor)
                    .Aggregate(0, HashCode.Combine);
                var stateHash = _observedState
                    .OrderBy(pair => pair.Key)
                    .Aggregate(0, HashCode.Combine);
                return HashCode.Combine(itemsHash, stateHash);
            }
        }

        public Dto ToDto()
        {
            lock (_setChangeLock)
            {
                return new Dto
                {
                    Items = _items
                        .Select(i => new VersionedSignedItem<TActorId, TItem>(i.Actor, i.Version, i.Item))
                        .ToHashSet(),
                    ObservedState = _observedState
                        .ToDictionary(pair => pair.Key, pair => pair.Value)
                };
            }
        }

        public HashSet<TItem> Value
        {
            get { lock(_setChangeLock) return _items.Select(i => i.Item).ToHashSet(); }
        }

        public void Add(TItem item, TActorId actorPerformingAddition)
        {
            lock (_setChangeLock)
            {
                // default value for int is 0, so if key is not preset, lastObservedVersion will be assigned 0, which is intended
                _observedState.TryGetValue(actorPerformingAddition, out var observedVersion);
                ++observedVersion;

                _items.Add(new VersionedSignedItem<TActorId, TItem>(actorPerformingAddition, observedVersion, item));

                // notice that i.Actor.Equals(actorPerformingAddition) means that there may be multiple copies of item
                // stored at the same time. This is by design
                _items.RemoveWhere(i => i.Item.Equals(item) && i.Version < observedVersion && i.Actor.Equals(actorPerformingAddition));
                _observedState[actorPerformingAddition] = observedVersion;
            }
        }

        public void Remove(TItem item)
        {
            lock (_setChangeLock)
            {
                _items.RemoveWhere(i => i.Item.Equals(item));
            }
        }

        public MergeResult Merge(OptimizedObservedRemoveSet<TActorId, TItem> other)
        {
            if (GetHashCode() == other.GetHashCode())
            {
                return MergeResult.Identical;
            }

            lock (_setChangeLock)
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

            return MergeResult.ConflictSolved;
        }

        [ProtoContract]
        public sealed class Dto
        {
            [ProtoMember(1)]
            public HashSet<VersionedSignedItem<TActorId, TItem>> Items { get; set; } = new();

            [ProtoMember(2)]
            public Dictionary<TActorId, uint> ObservedState { get; set; } = new();
        }

        public sealed class Factory : ICRDTFactory<OptimizedObservedRemoveSet<TActorId, TItem>, HashSet<TItem>, Dto>
        {
            /// <inheritdoc />
            public OptimizedObservedRemoveSet<TActorId, TItem> Create(Dto dto) => FromDto(dto);
        }
    }
}
