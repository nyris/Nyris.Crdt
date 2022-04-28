using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Model.Converters;
using Nyris.Crdt.Model;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    /// <summary>
    /// Encapsulates Guid
    /// </summary>
    [JsonConverter(typeof(InternalIdJsonConverter<CollectionId, Factory>))]
    [TypeConverter(typeof(InternalIdTypeConverter<CollectionId, Factory>))]
    [ProtoContract]
    public readonly struct CollectionId : IEquatable<CollectionId>, IFormattable, IComparable<CollectionId>, IAs<Guid>,
        IHashable
    {
        /// <summary>
        /// Converts guid into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static CollectionId FromGuid(Guid id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static CollectionId New() => new(Guid.NewGuid());

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">input – The string to convert.</param>
        /// <returns></returns>
        public static CollectionId Parse(string input) => new(Guid.Parse(input));

        /// <summary>
        /// A read-only instance of the NodeId structure, that can represent default or uninitialized value.
        /// </summary>
        public static readonly CollectionId Empty = new(Guid.Empty);

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">A string containing the NodeId to convert</param>
        /// <param name="collectionId">An NodeId instance to contain the parsed value. If the method returns true,
        /// result contains a valid NodeId. If the method returns false, result equals Empty.</param>
        /// <returns>true if the parse operation was successful; otherwise, false.</returns>
        public static bool TryParse(string input, [NotNullWhen(true)] out CollectionId collectionId)
        {
            if (Guid.TryParse(input, out var guid))
            {
                collectionId = FromGuid(guid);
                return true;
            }

            collectionId = Empty;
            return false;
        }

        [ProtoMember(1)] private readonly Guid _id;

        private CollectionId(Guid id)
        {
            _id = id;
        }

        /// <inheritdoc />
        public Guid Value => _id;

        /// <inheritdoc />
        public bool Equals(CollectionId other) => _id.Equals(other._id);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CollectionId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => _id.ToString(format, formatProvider);

        /// <inheritdoc />
        public override string ToString() => _id.ToString("N");

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();

        /// <inheritdoc />
        public int CompareTo(CollectionId other) => _id.CompareTo(other._id);

        public static bool operator ==(CollectionId left, CollectionId right) => left.Equals(right);

        public static bool operator !=(CollectionId left, CollectionId right) => !(left == right);

        private class Factory : IFactory<CollectionId>
        {
            CollectionId IFactory<CollectionId>.Empty => Empty;
            CollectionId IFactory<CollectionId>.Parse(string value) => CollectionId.Parse(value);
        }
    }
}
