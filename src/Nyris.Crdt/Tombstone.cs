using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace Nyris.Crdt;

/// <summary>
/// Similar to <seealso cref="Dot{TActorId}"/> of an Value
/// but a <seealso cref="Tombstone{TActorId}"/> represents a deletion of an Value
/// </summary>
/// <typeparam name="TActorId"></typeparam>
[ProtoContract]
public readonly struct Tombstone<TActorId> : IEquatable<Tombstone<TActorId>>, IEquatable<Dot<TActorId>>
    where TActorId : IEquatable<TActorId>
{
    public Tombstone(Dot<TActorId> dot)
    {
        ObservedByActors = new HashSet<TActorId>
        {
            dot.Actor
        };
        Dot = dot;
    }

    /// <summary>
    /// <seealso cref="Dot"/> of the deleted Value
    /// </summary>
    [ProtoMember(1)]
    public Dot<TActorId> Dot { get; }

    /// <summary>
    /// List of <seealso cref="TActorId"/> that have seen/processed this <seealso cref="Tombstone{TActorId}"/> i.e deletion
    /// </summary>
    [ProtoMember(2)]
    public IEnumerable<TActorId> ObservedByActors { get; }

    /// <summary>
    /// Determines if all the <seealso cref="TActorId"/> have seen/processed this  <seealso cref="Tombstone{TActorId}"/> i.e deletion
    /// </summary>
    /// <param name="allKnownActors"></param>
    /// <returns></returns>
    public bool CanBeDiscarded(IEnumerable<TActorId> allKnownActors) =>
        allKnownActors.SequenceEqual(ObservedByActors);

    public override string ToString() =>
        $"(Dot: {Dot} ActorsAck: {ObservedByActors.Aggregate("", (aggregate, id) => $"{aggregate}{id}, ")})";

    public bool Equals(Tombstone<TActorId> other) =>
        ObservedByActors.SequenceEqual(other.ObservedByActors) && Dot.Equals(other.Dot);

    public bool Equals(Dot<TActorId> other) => other.Equals(Dot);

    public override bool Equals(object? obj) => obj is Tombstone<TActorId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ObservedByActors, Dot);

    public static bool operator ==(Tombstone<TActorId> left, Tombstone<TActorId> right) => left.Equals(right);

    public static bool operator !=(Tombstone<TActorId> left, Tombstone<TActorId> right) => !left.Equals(right);

    public static bool operator ==(Tombstone<TActorId> left, Dot<TActorId> right) => left.Equals(right);

    public static bool operator !=(Tombstone<TActorId> left, Dot<TActorId> right) => !left.Equals(right);
}
