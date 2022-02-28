using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model.Converters;
using Nyris.Extensions.Guids;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Encapsulates Guid
    /// </summary>
    [JsonConverter(typeof(InternalIdJsonConverter<ShardId, Factory>))]
    [TypeConverter(typeof(InternalIdTypeConverter<ShardId, Factory>))]
    [ProtoContract]
    public readonly struct ShardId : IEquatable<ShardId>, IFormattable, IComparable<ShardId>, IAs<Guid>, IHashable
    {
        /// <summary>
        /// Converts guid into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static ShardId FromGuid(Guid id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static ShardId New() => new(Guid.NewGuid());

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">input – The string to convert.</param>
        /// <returns></returns>
        public static ShardId Parse(string input) => new(ShortGuid.Decode(input));

        /// <summary>
        /// A read-only instance of the NodeId structure, that can represent default or uninitialized value.
        /// </summary>
        public static readonly ShardId Empty = new(Guid.Empty);

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">A string containing the NodeId to convert</param>
        /// <param name="shardId">An NodeId instance to contain the parsed value. If the method returns true,
        /// result contains a valid NodeId. If the method returns false, result equals Empty.</param>
        /// <returns>true if the parse operation was successful; otherwise, false.</returns>
        public static bool TryParse(string input, [NotNullWhen(true)] out ShardId shardId)
        {
            if (ShortGuid.TryParse(input, out Guid guid))
            {
                shardId = FromGuid(guid);
                return true;
            }

            shardId = Empty;
            return false;
        }

        [ProtoMember(1)]
        private readonly Guid _id;

        private ShardId(Guid id)
        {
            _id = id;
        }

        /// <inheritdoc />
        public Guid Value => _id;

        /// <inheritdoc />
        public bool Equals(ShardId other) => _id.Equals(other._id);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ShardId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

        /// <inheritdoc />
        public override string ToString() => ShortGuid.Encode(_id);

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();

        /// <inheritdoc />
        public int CompareTo(ShardId other) => _id.CompareTo(other._id);

        public static bool operator ==(ShardId left, ShardId right) => left.Equals(right);

        public static bool operator !=(ShardId left, ShardId right) => !(left == right);

        private class Factory : IFactory<ShardId>
        {
            ShardId IFactory<ShardId>.Empty => Empty;
            ShardId IFactory<ShardId>.Parse(string value) => ShardId.Parse(value);
        }
    }
}
