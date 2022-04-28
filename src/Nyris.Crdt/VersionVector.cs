using System;
using Nyris.Crdt.Model;
using ProtoBuf;

namespace Nyris.Crdt;

/// <summary>
/// Dot Vector represents the current state of a Node/Actor in cluster
/// </summary>
/// <typeparam name="TActorId"></typeparam>
[ProtoContract]
public readonly struct VersionVector<TActorId> : IEquatable<VersionVector<TActorId>>, IEquatable<Dot<TActorId>>,
    IHashable
    where TActorId : IEquatable<TActorId>
{
    public VersionVector(TActorId actor, uint version)
    {
        Version = version;
        Actor = actor;
    }

    /// <summary>
    /// A monotonic counter that represents the number of operations performed by of Node/Actor
    /// </summary>
    [ProtoMember(2)]
    public uint Version { get; }

    /// <summary>
    /// Node/Actor who's state this Dot represents
    /// </summary>
    [ProtoMember(3)]
    public TActorId Actor { get; }

    /// <summary>
    /// Create the next version of <seealso cref="VersionVector{TActorId}"/> from current version
    /// </summary>
    /// <returns></returns>
    public VersionVector<TActorId> Next() => new(Actor, Version + 1);

    public override string ToString() => $"(v: {Version} a: {Actor})";

    public ReadOnlySpan<byte> CalculateHash() => BitConverter.GetBytes(Version);

    public bool Equals(VersionVector<TActorId> other) => Version == other.Version && Actor.Equals(other.Actor);
    public bool Equals(Dot<TActorId> other) => Version == other.Version && Actor.Equals(other.Actor);

    public override bool Equals(object? obj) => obj is VersionVector<TActorId> other && Equals(other);

    public bool GreaterThan(VersionVector<TActorId> other) => Actor.Equals(other.Actor) && Version > other.Version;
    public bool LessThan(VersionVector<TActorId> other) => Actor.Equals(other.Actor) && Version < other.Version;

    public override int GetHashCode() => HashCode.Combine(Version, Actor);

    public static bool operator ==(VersionVector<TActorId> left, VersionVector<TActorId> right) => left.Equals(right);
    public static bool operator !=(VersionVector<TActorId> left, VersionVector<TActorId> right) => !left.Equals(right);

    public static bool operator ==(VersionVector<TActorId> left, Dot<TActorId> right) => left.Equals(right);
    public static bool operator !=(VersionVector<TActorId> left, Dot<TActorId> right) => !left.Equals(right);

    public static bool operator >(VersionVector<TActorId> left, VersionVector<TActorId> right) =>
        left.GreaterThan(right);

    public static bool operator <(VersionVector<TActorId> left, VersionVector<TActorId> right) => !left.LessThan(right);
}
