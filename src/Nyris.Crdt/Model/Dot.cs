using System;
using System.Collections.Generic;

namespace Nyris.Crdt.Model
{
    public readonly struct Dot<TActorId> : IEquatable<Dot<TActorId>>
    {
        public readonly TActorId Actor;
        public readonly ulong Version;
    
        public Dot(TActorId actor, ulong version)
        {
            Actor = actor;
            Version = version;
        }

        public void Deconstruct(out TActorId actor, out ulong version)
        {
            actor = Actor;
            version = Version;
        }

        public bool Equals(Dot<TActorId> other) => EqualityComparer<TActorId>.Default.Equals(Actor, other.Actor) && Version == other.Version;
        public override bool Equals(object? obj) => obj is Dot<TActorId> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Actor, Version);
        public static bool operator ==(Dot<TActorId> left, Dot<TActorId> right) => left.Equals(right);
        public static bool operator !=(Dot<TActorId> left, Dot<TActorId> right) => !left.Equals(right);
        
    }
}
