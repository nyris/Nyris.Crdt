using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model.Converters;
using Nyris.Extensions.Guids;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Represents an NodeId structure, which encapsulates Guid and allows to explicitly separate ids for different entities
    /// </summary>
    [JsonConverter(typeof(InternalIdJsonConverter<NodeId, Factory>))]
    [TypeConverter(typeof(InternalIdTypeConverter<NodeId, Factory>))]
    [ProtoContract]
    public readonly struct NodeId : IEquatable<NodeId>, IFormattable, IComparable<NodeId>, IHashable
    {
        /// <summary>
        /// Converts guid into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static NodeId FromString(string id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static NodeId New() => new(ShortGuid.Encode(Guid.NewGuid()));

        /// <summary>
        /// A read-only instance of the NodeId structure, that can represent default or uninitialized value.
        /// </summary>
        public static readonly NodeId Empty = new("");

        [ProtoMember(1)]
        private readonly string _id;

        private NodeId(string id)
        {
            _id = id;
        }

        /// <inheritdoc />
        public bool Equals(NodeId other) => _id.Equals(other._id);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is NodeId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

        /// <inheritdoc />
        public override string ToString() => _id;

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => Encoding.Default.GetBytes(_id);

        /// <inheritdoc />
        public int CompareTo(NodeId other) => string.Compare(_id, other._id, StringComparison.Ordinal);

        public static bool operator ==(NodeId left, NodeId right) => left.Equals(right);

        public static bool operator !=(NodeId left, NodeId right) => !(left == right);

        private class Factory : IFactory<NodeId>
        {
            NodeId IFactory<NodeId>.Empty => Empty;
            NodeId IFactory<NodeId>.Parse(string value) => new(value);
        }
    }

    /// <summary>
    /// Represents an GNodeId structure, which encapsulates Guid and allows to explicitly separate ids for different entities
    /// </summary>
    [JsonConverter(typeof(InternalIdJsonConverter<GNodeId, Factory>))]
    [TypeConverter(typeof(InternalIdTypeConverter<GNodeId, Factory>))]
    [ProtoContract]
    public readonly struct GNodeId : IEquatable<GNodeId>, IFormattable, IComparable<GNodeId>, IAs<Guid>, IHashable
    {
        /// <summary>
        /// Converts guid into GNodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static GNodeId FromGuid(Guid id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static GNodeId New() => new(Guid.NewGuid());

        /// <summary>
        /// Converts the string representation of an GNodeId to the equivalent GNodeId structure.
        /// </summary>
        /// <param name="input">input – The string to convert.</param>
        /// <returns></returns>
        public static GNodeId Parse(string input) => new(Guid.Parse(input));

        /// <summary>
        /// A read-only instance of the GNodeId structure, that can represent default or uninitialized value.
        /// </summary>
        public static readonly GNodeId Empty = new(Guid.Empty);

        /// <summary>
        /// Converts the string representation of an GNodeId to the equivalent GNodeId structure.
        /// </summary>
        /// <param name="input">A string containing the GNodeId to convert</param>
        /// <param name="indexId">An GNodeId instance to contain the parsed value. If the method returns true,
        /// result contains a valid GNodeId. If the method returns false, result equals Empty.</param>
        /// <returns>true if the parse operation was successful; otherwise, false.</returns>
        public static bool TryParse(string input, [NotNullWhen(true)] out GNodeId indexId)
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

        private GNodeId(Guid id)
        {
            _id = id;
        }

        /// <inheritdoc />
        public Guid Value => _id;

        /// <inheritdoc />
        public bool Equals(GNodeId other) => _id.Equals(other._id);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is GNodeId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => _id.ToString(format, formatProvider);

        /// <inheritdoc />
        public override string ToString() => _id.ToString("N");

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();

        /// <inheritdoc />
        public int CompareTo(GNodeId other) => _id.CompareTo(other._id);

        public static bool operator ==(GNodeId left, GNodeId right) => left.Equals(right);

        public static bool operator !=(GNodeId left, GNodeId right) => !(left == right);

        private class Factory : IFactory<GNodeId>
        {
            GNodeId IFactory<GNodeId>.Empty => Empty;
            GNodeId IFactory<GNodeId>.Parse(string value) => GNodeId.Parse(value);
        }
    }
}
