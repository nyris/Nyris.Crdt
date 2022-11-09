using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;
using System;
using System.Linq;
using Nyris.Crdt.Interfaces;
#pragma warning disable CS0618

namespace Nyris.Crdt.Distributed.Crdts;

public sealed class
    HashableOptimizedObservedRemoveSet<TActorId, TItem> : OptimizedObservedRemoveSet<TActorId, TItem>, IHashable
    where TItem : IEquatable<TItem>
    where TActorId : IEquatable<TActorId>, IComparable<TActorId>
{
    public HashableOptimizedObservedRemoveSet() { }

    private HashableOptimizedObservedRemoveSet(OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto)
        : base(optimizedObservedRemoveSetDto) { }

    /// <inheritdoc />
    public ReadOnlySpan<byte> CalculateHash()
    {
        lock (SetChangeLock)
        {
            return HashingHelper.Combine(
                                         HashingHelper.Combine(Items.OrderBy(item => item.Dot.Actor)),
                                         HashingHelper.Combine(VersionVectors.OrderBy(pair => pair.Key)));
        }
    }

    public new static HashableOptimizedObservedRemoveSet<TActorId, TItem> FromDto(
        OptimizedObservedRemoveSetDto optimizedObservedRemoveSetDto
    )
        => new(optimizedObservedRemoveSetDto);

    public new sealed class Factory : ICRDTFactory<HashableOptimizedObservedRemoveSet<TActorId, TItem>,
        OptimizedObservedRemoveSetDto>
    {
        /// <inheritdoc />
        public HashableOptimizedObservedRemoveSet<TActorId, TItem> Create(OptimizedObservedRemoveSetDto dto) => FromDto(dto);
    }
}
