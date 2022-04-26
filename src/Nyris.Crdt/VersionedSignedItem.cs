using System;
using ProtoBuf;

namespace Nyris.Crdt
{
    [ProtoContract]
    public readonly struct VersionedSignedItem<TActorId, TItem> : IEquatable<VersionedSignedItem<TActorId, TItem>>
        where TActorId : IEquatable<TActorId>
        where TItem : IEquatable<TItem>
    {
        public VersionedSignedItem(TActorId actor, uint version, TItem item)
        {
            Actor = actor;
            Version = version;
            Item = item;
        }

        [ProtoMember(1)]
        public TItem Item { get; }

        /// <summary>
        /// Concept of Tag of Item
        /// <para />
        /// Actor + Version = Tag
        /// </summary>
        [ProtoMember(2)]
        public uint Version { get; }

        /// <summary>
        /// Concept of Tag of Item
        /// <para />
        /// Actor + Version = Tag
        /// </summary>
        [ProtoMember(3)]
        public TActorId Actor { get; }

        public override string ToString() => $"({Item.ToString()}, v: {Version}, a: {Actor})";
        public bool Equals(VersionedSignedItem<TActorId, TItem> other)
            => Item.Equals(other.Item) && Version == other.Version && Actor.Equals(other.Actor);
        public override bool Equals(object obj) => obj is VersionedSignedItem<TActorId, TItem> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Item, Version, Actor);
        public static bool operator ==(VersionedSignedItem<TActorId, TItem> left, VersionedSignedItem<TActorId, TItem> right) => left.Equals(right);
        public static bool operator !=(VersionedSignedItem<TActorId, TItem> left, VersionedSignedItem<TActorId, TItem> right) => !left.Equals(right);
    }
}