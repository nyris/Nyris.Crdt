using System;
using System.Linq;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Distributed.Crdts
{
    internal sealed class HashableOptimizedObservedRemoveSet<TActorId, TItem> : OptimizedObservedRemoveSet<TActorId, TItem>, IHashable
        where TItem : IEquatable<TItem>, IHashable
        where TActorId : IEquatable<TActorId>, IHashable
    {
        public HashableOptimizedObservedRemoveSet()
        {
        }

        private HashableOptimizedObservedRemoveSet(Dto dto) : base(dto)
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

        public new static HashableOptimizedObservedRemoveSet<TActorId, TItem> FromDto(Dto dto) => new(dto);
    }
}