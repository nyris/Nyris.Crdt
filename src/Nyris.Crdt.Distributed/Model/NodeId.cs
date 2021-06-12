using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Model.Converters;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    public interface IAs<out T>
    {
        public T Value { get; }
    }

    /// <summary>
    /// Represents an NodeId structure, which encapsulates Guid and allows to explicitly separate ids for different entities
    /// </summary>
    [JsonConverter(typeof(InternalIdJsonConverter<NodeId, Factory>))]
    [TypeConverter(typeof(InternalIdTypeConverter<NodeId, Factory>))]
    [ProtoContract]
    public readonly struct NodeId : IEquatable<NodeId>, IFormattable, IComparable<NodeId>, IAs<Guid>
    {
        /// <summary>
        /// Converts guid into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static NodeId FromGuid(Guid id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static NodeId New() => new(Guid.NewGuid());

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">input – The string to convert.</param>
        /// <returns></returns>
        public static NodeId Parse(string input) => new(Guid.Parse(input));

        /// <summary>
        /// A read-only instance of the NodeId structure, that can represent default or uninitialized value.
        /// </summary>
        public static readonly NodeId Empty = new(Guid.Empty);

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">A string containing the NodeId to convert</param>
        /// <param name="indexId">An NodeId instance to contain the parsed value. If the method returns true,
        /// result contains a valid NodeId. If the method returns false, result equals Empty.</param>
        /// <returns>true if the parse operation was successful; otherwise, false.</returns>
        public static bool TryParse(string input, [NotNullWhen(true)] out NodeId indexId)
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

        private NodeId(Guid id)
        {
            _id = id;
        }

        /// <inheritdoc />
        public Guid Value => _id;

        /// <inheritdoc />
        public bool Equals(NodeId other) => _id.Equals(other._id);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is NodeId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _id.GetHashCode();

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => _id.ToString(format, formatProvider);

        /// <inheritdoc />
        public override string ToString() => _id.ToString("N");

        /// <inheritdoc />
        public int CompareTo(NodeId other) => _id.CompareTo(other._id);

        public static bool operator ==(NodeId left, NodeId right) => left.Equals(right);

        public static bool operator !=(NodeId left, NodeId right) => !(left == right);

        private class Factory : IFactory<NodeId>
        {
            NodeId IFactory<NodeId>.Empty => Empty;
            NodeId IFactory<NodeId>.Parse(string value) => NodeId.Parse(value);
        }
    }
}
