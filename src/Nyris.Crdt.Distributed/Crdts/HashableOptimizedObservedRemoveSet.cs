using System;
using System.Linq;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class HashableOptimizedObservedRemoveSet<TActorId, TItem> : OptimizedObservedRemoveSet<TActorId, TItem>, IHashable
        where TItem : IEquatable<TItem>
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
    {
        public HashableOptimizedObservedRemoveSet()
        {
        }

        private HashableOptimizedObservedRemoveSet(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto)
            : base(optimizedObservedRemoveSetDto)
        {
        }

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash()
        {
            lock (SetChangeLock)
            {
                return HashingHelper.Combine(
                    HashingHelper.Combine(Items.OrderBy(item => item.Actor)),
                    HashingHelper.Combine(ObservedState.OrderBy(pair => pair.Key)));
            }
        }

        public new static HashableOptimizedObservedRemoveSet<TActorId, TItem> FromDto(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto)
            => new(optimizedObservedRemoveSetDto);

        public new sealed class Factory : ICRDTFactory<HashableOptimizedObservedRemoveSet<TActorId, TItem>, OptimizedObservedRemoveSetDto>
        {
            /// <inheritdoc />
            public HashableOptimizedObservedRemoveSet<TActorId, TItem> Create(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto)
                => FromDto(optimizedObservedRemoveSetDto);
        }
    }
}