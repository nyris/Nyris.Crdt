using System;
using ProtoBuf;

namespace Nyris.Crdt.Model;

/// <summary>
/// It represents the state of <seealso cref="TActorId"/> when the Value this <seealso cref="IntDot{TActorId}"/> represents was created, like a timestamp.
/// Similar to Dot Vector but conceptually different. A <seealso cref="IntDot{TActorId}"/> attaches to an Value
/// </summary>
/// <typeparam name="TActorId"></typeparam>
[ProtoContract]
public readonly struct IntDot<TActorId> : IEquatable<IntDot<TActorId>>
    where TActorId : IEquatable<TActorId>
{
    public IntDot(TActorId actor, uint version)
    {
        Version = version;
        Actor = actor;
    }

    /// <summary>
    /// A monotonic counter that represents the number of operations performed by of Node/Actor
    /// </summary>
    [ProtoMember(1)]
    public uint Version { get; }

    /// <summary>
    /// Node/Actor who's state this Dot represents
    /// </summary>
    [ProtoMember(2)]
    public TActorId Actor { get; }

    public override string ToString() => $"(v: {Version} a: {Actor})";

    public bool Equals(IntDot<TActorId> other) => Actor.Equals(other.Actor) && Version == other.Version;

    public override bool Equals(object? obj) => obj is IntDot<TActorId> other && Equals(other);

    public bool GreaterThan(IntDot<TActorId> other) => Actor.Equals(other.Actor) && Version > other.Version;
    public bool LessThan(IntDot<TActorId> other) => Actor.Equals(other.Actor) && Version < other.Version;

    /// <inheritdoc cref="uint.CompareTo(uint)"/>
    public int CompareTo(IntDot<TActorId> other) => Actor.Equals(other.Actor) ? Version.CompareTo(other.Version) : -1;

    public override int GetHashCode() => HashCode.Combine(Version, Actor.GetHashCode());

    public static bool operator ==(IntDot<TActorId> left, IntDot<TActorId> right) => left.Equals(right);
    public static bool operator !=(IntDot<TActorId> left, IntDot<TActorId> right) => !left.Equals(right);

    public static bool operator >(IntDot<TActorId> left, IntDot<TActorId> right) => left.GreaterThan(right);
    public static bool operator <(IntDot<TActorId> left, IntDot<TActorId> right) => left.LessThan(right);
}
