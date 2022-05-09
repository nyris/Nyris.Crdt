using System;
using ProtoBuf;

namespace Nyris.Crdt;

/// <summary>
/// It represents the state of <seealso cref="TActorId"/> when the Value this <seealso cref="Dot{TActorId}"/> represents was created, like a timestamp.
/// Similar to Dot Vector but conceptually different. A <seealso cref="Dot{TActorId}"/> attaches to an Value
/// </summary>
/// <typeparam name="TActorId"></typeparam>
[ProtoContract]
public readonly struct Dot<TActorId> : IEquatable<Dot<TActorId>>
    where TActorId : IEquatable<TActorId>
{
    public Dot(TActorId actor, uint version)
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

    public bool Equals(Dot<TActorId> other) => Actor.Equals(other.Actor) && Version == other.Version;

    public override bool Equals(object? obj) => obj is Dot<TActorId> other && Equals(other);

    public bool GreaterThan(Dot<TActorId> other) => Actor.Equals(other.Actor) && Version > other.Version;
    public bool LessThan(Dot<TActorId> other) => Actor.Equals(other.Actor) && Version < other.Version;

    public override int GetHashCode() => HashCode.Combine(Version, Actor.GetHashCode());

    public static bool operator ==(Dot<TActorId> left, Dot<TActorId> right) => left.Equals(right);
    public static bool operator !=(Dot<TActorId> left, Dot<TActorId> right) => !left.Equals(right);

    public static bool operator >(Dot<TActorId> left, Dot<TActorId> right) => left.GreaterThan(right);
    public static bool operator <(Dot<TActorId> left, Dot<TActorId> right) => left.LessThan(right);
}
