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
    public class OptimizedObservedRemoveSet<TActorId, TItem>
        : ICRDT<
            OptimizedObservedRemoveSet<TActorId, TItem>,
            HashSet<TItem>,
            OptimizedObservedRemoveSet<TActorId, TItem>.OptimizedObservedRemoveSetDto>
        where TItem : IEquatable<TItem>
        where TActorId : IEquatable<TActorId>
    {
        protected HashSet<VersionedSignedItem<TActorId, TItem>> Items;
        protected readonly Dictionary<TActorId, uint> ObservedState;
        protected readonly object SetChangeLock = new();

        public OptimizedObservedRemoveSet()
        {
            Items = new HashSet<VersionedSignedItem<TActorId, TItem>>();
            ObservedState = new Dictionary<TActorId, uint>();
        }

        protected OptimizedObservedRemoveSet(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto)
        {
            Items = optimizedObservedRemoveSetDto.Items;
            ObservedState = optimizedObservedRemoveSetDto.ObservedState;
        }

        public static OptimizedObservedRemoveSet<TActorId, TItem> FromDto(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto)
            => new(optimizedObservedRemoveSetDto);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            lock (SetChangeLock)
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                var itemsHash = Items
                    .OrderBy(i => i.Actor)
                    .Aggregate(0, HashCode.Combine);
                var stateHash = ObservedState
                    .OrderBy(pair => pair.Key)
                    .Aggregate(0, HashCode.Combine);
                return HashCode.Combine(itemsHash, stateHash);
            }
        }

        public OptimizedObservedRemoveSetDto ToDto()
        {
            lock (SetChangeLock)
            {
                return new OptimizedObservedRemoveSetDto
                {
                    Items = Items
                        .Select(i => new VersionedSignedItem<TActorId, TItem>(i.Actor, i.Version, i.Item))
                        .ToHashSet(),
                    ObservedState = ObservedState
                        .ToDictionary(pair => pair.Key, pair => pair.Value)
                };
            }
        }

        public HashSet<TItem> Value
        {
            get { lock(SetChangeLock) return Items.Select(i => i.Item).ToHashSet(); }
        }

        public bool Contains(TItem item)
        {
            lock(SetChangeLock) return Items.Any(i => i.Item.Equals(item));
        }

        public void Add(TItem item, TActorId actorPerformingAddition)
        {
            lock (SetChangeLock)
            {
                // default value for int is 0, so if key is not preset, lastObservedVersion will be assigned 0, which is intended
                ObservedState.TryGetValue(actorPerformingAddition, out var observedVersion);
                ++observedVersion;

                Items.Add(new VersionedSignedItem<TActorId, TItem>(actorPerformingAddition, observedVersion, item));

                // notice that i.Actor.Equals(actorPerformingAddition) means that there may be multiple copies of item
                // stored at the same time. This is by design
                Items.RemoveWhere(i => i.Item.Equals(item) && i.Version < observedVersion && i.Actor.Equals(actorPerformingAddition));
                ObservedState[actorPerformingAddition] = observedVersion;
            }
        }

        public void Remove(TItem item)
        {
            lock (SetChangeLock)
            {
                Items.RemoveWhere(i => i.Item.Equals(item));
            }
        }

        public MergeResult Merge(OptimizedObservedRemoveSet<TActorId, TItem> other)
        {
            if (GetHashCode() == other.GetHashCode())
            {
                return MergeResult.Identical;
            }

            lock (SetChangeLock)
            {
                // variables names a taken from the paper, they do not have obvious meaning by themselves
                var m = Items.Intersect(other.Items);

                var m1 = Items
                    .Except(other.Items)
                    .Where(i => !other.ObservedState.TryGetValue(i.Actor, out var otherVersion)
                                || i.Version > otherVersion);

                var m2 = other.Items
                    .Except(Items)
                    .Where(i => !ObservedState.TryGetValue(i.Actor, out var myVersion)
                                || i.Version > myVersion);

                var u = m.Union(m1).Union(m2);

                // TODO: maybe make it faster then O(n^2)?
                var o = Items
                    .Where(item => Items.Any(i => item.Item.Equals(i.Item)
                                                   && item.Actor.Equals(i.Actor)
                                                   && item.Version < i.Version));

                Items = u.Except(o).ToHashSet();

                // observed state is a element-wise max of two vectors.
                foreach (var actorId in ObservedState.Keys.ToList().Union(other.ObservedState.Keys))
                {
                    ObservedState.TryGetValue(actorId, out var thisVersion);
                    other.ObservedState.TryGetValue(actorId, out var otherVersion);
                    ObservedState[actorId] = thisVersion > otherVersion ? thisVersion : otherVersion;
                }
            }

            return MergeResult.ConflictSolved;
        }

        [ProtoContract]
        public sealed class OptimizedObservedRemoveSetDto
        {
            [ProtoMember(1)]
            public HashSet<VersionedSignedItem<TActorId, TItem>> Items { get; set; } = new();

            [ProtoMember(2)]
            public Dictionary<TActorId, uint> ObservedState { get; set; } = new();
        }

        public sealed class Factory : ICRDTFactory<OptimizedObservedRemoveSet<TActorId, TItem>, HashSet<TItem>, OptimizedObservedRemoveSetDto>
        {
            /// <inheritdoc />
            public OptimizedObservedRemoveSet<TActorId, TItem> Create(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto) => FromDto(optimizedObservedRemoveSetDto);
        }
    }
}
