using ProtoBuf;
using System;

namespace Nyris.Crdt;

/// <summary>
/// <seealso cref="TItem"/> that has an attached <seealso cref="Dot{TActorId}"/> with it
/// </summary>
/// <typeparam name="TActorId"></typeparam>
/// <typeparam name="TItem"></typeparam>
[ProtoContract]
public readonly struct DottedItemWithActor<TActorId, TItem> : IEquatable<DottedItemWithActor<TActorId, TItem>>
    where TActorId : IEquatable<TActorId>
    where TItem : IEquatable<TItem>
{
    public DottedItemWithActor(Dot<TActorId> dot, TItem value)
    {
        Dot = dot;
        Value = value;
    }

    /// <summary>
    /// Value of current <seealso cref="DottedItemWithActor{TActorId,TItem}"/>
    /// </summary>
    [ProtoMember(1)]
    public TItem Value { get; }

    /// <inheritdoc cref="Dot{TActorId}" />
    [ProtoMember(2)]
    public Dot<TActorId> Dot { get; }

    public override string ToString() => $"({Value}, v: {Dot})";

    public bool Equals(DottedItemWithActor<TActorId, TItem> other)
        => Value.Equals(other.Value) && Dot.Equals(other.Dot);

    public override bool Equals(object? obj) => obj is DottedItemWithActor<TActorId, TItem> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value.GetHashCode(), Dot.GetHashCode());

    public static bool operator ==(
        DottedItemWithActor<TActorId, TItem> left,
        DottedItemWithActor<TActorId, TItem> right
    ) => left.Equals(right);

    public static bool operator !=(
        DottedItemWithActor<TActorId, TItem> left,
        DottedItemWithActor<TActorId, TItem> right
    ) => !left.Equals(right);
}
