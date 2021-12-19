using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Model.Converters;
using ProtoBuf;

namespace Nyris.Crdt.GrpcServiceSample
{
    /// <summary>
    /// Represents an NodeId structure, which encapsulates Guid and allows to explicitly separate ids for different entities
    /// </summary>
    [JsonConverter(typeof(InternalIdJsonConverter<IndexId, Factory>))]
    [TypeConverter(typeof(InternalIdTypeConverter<IndexId, Factory>))]
    [ProtoContract]
    public readonly struct IndexId : IEquatable<IndexId>, IFormattable, IComparable<IndexId>, IAs<Guid>, IHashable
    {
        /// <summary>
        /// Converts guid into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static IndexId FromGuid(Guid id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static IndexId New() => new(Guid.NewGuid());

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">input – The string to convert.</param>
        /// <returns></returns>
        public static IndexId Parse(string input) => new(Guid.Parse(input));

        /// <summary>
        /// A read-only instance of the NodeId structure, that can represent default or uninitialized value.
        /// </summary>
        public static readonly IndexId Empty = new(Guid.Empty);

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">A string containing the NodeId to convert</param>
        /// <param name="indexId">An NodeId instance to contain the parsed value. If the method returns true,
        /// result contains a valid NodeId. If the method returns false, result equals Empty.</param>
        /// <returns>true if the parse operation was successful; otherwise, false.</returns>
        public static bool TryParse(string input, [NotNullWhen(true)] out IndexId indexId)
        {
            if (Guid.TryParse(input, out var guid))
            {
                indexId = FromGuid(guid);
                return true;
            }

            indexId = Empty;
            return false;
        }

        [ProtoMember(1)]
        private readonly Guid _id;

        private IndexId(Guid id)
        {
            _id = id;
        }

        /// <inheritdoc />
        public Guid Value => _id;

        /// <inheritdoc />
        public bool Equals(IndexId other) => _id.Equals(other._id);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is IndexId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => _id.ToString(format, formatProvider);

        /// <inheritdoc />
        public override string ToString() => _id.ToString("N");

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();

        /// <inheritdoc />
        public int CompareTo(IndexId other) => _id.CompareTo(other._id);

        public static bool operator ==(IndexId left, IndexId right) => left.Equals(right);

        public static bool operator !=(IndexId left, IndexId right) => !(left == right);

        private class Factory : IFactory<IndexId>
        {
            IndexId IFactory<IndexId>.Empty => Empty;
            IndexId IFactory<IndexId>.Parse(string value) => IndexId.Parse(value);
        }
    }
}
