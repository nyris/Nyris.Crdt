using System;
using ProtoBuf;

namespace Nyris.Crdt;

/// <summary>
/// <seealso cref="TItem"/> that has an attached <seealso cref="Dot{TActorId}"/> with it
/// </summary>
/// <typeparam name="TActorId"></typeparam>
/// <typeparam name="TItem"></typeparam>
[ProtoContract]
public readonly struct DottedItem<TActorId, TItem> : IEquatable<DottedItem<TActorId, TItem>>,
    IEquatable<VersionVector<TActorId>>, IEquatable<Tombstone<TActorId>>
    where TActorId : IEquatable<TActorId>
    where TItem : IEquatable<TItem>
{
    public DottedItem(Dot<TActorId> dot, TItem value)
    {
        Dot = dot;
        Value = value;
    }

    /// <summary>
    /// Value of current <seealso cref="DottedItem{TActorId,TItem}"/>
    /// </summary>
    [ProtoMember(1)]
    public TItem Value { get; }

    /// <inheritdoc cref="Dot{TActorId}" />
    [ProtoMember(2)]
    public Dot<TActorId> Dot { get; }

    public override string ToString() => $"({Value}, v: {Dot})";

    public bool Equals(DottedItem<TActorId, TItem> other)
        => Value.Equals(other.Value) && Dot.Equals(other.Dot);

    public bool Equals(VersionVector<TActorId> other) => Dot.Equals(other);

    public bool Equals(Tombstone<TActorId> other) => Dot.Equals(other.Dot);

    public override bool Equals(object? obj) => obj is DottedItem<TActorId, TItem> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value.GetHashCode(), Dot.GetHashCode());

    public static bool operator ==(DottedItem<TActorId, TItem> left,
        DottedItem<TActorId, TItem> right) => left.Equals(right);

    public static bool operator !=(DottedItem<TActorId, TItem> left,
        DottedItem<TActorId, TItem> right) => !left.Equals(right);

    public static bool operator ==(DottedItem<TActorId, TItem> left,
        VersionVector<TActorId> right) => left.Equals(right);

    public static bool operator !=(DottedItem<TActorId, TItem> left,
        VersionVector<TActorId> right) => !left.Equals(right);

    public static bool operator ==(DottedItem<TActorId, TItem> left,
        Tombstone<TActorId> right) => left.Equals(right);

    public static bool operator !=(DottedItem<TActorId, TItem> left,
        Tombstone<TActorId> right) => !left.Equals(right);
}
