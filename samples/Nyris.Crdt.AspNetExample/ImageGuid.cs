using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Model.Converters;
using Nyris.Crdt.Model;
using ProtoBuf;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Nyris.Crdt.AspNetExample;

/// <summary>
/// Encapsulates Guid
/// </summary>
[JsonConverter(typeof(InternalIdJsonConverter<ImageGuid, Factory>))]
[TypeConverter(typeof(InternalIdTypeConverter<ImageGuid, Factory>))]
[ProtoContract]
public readonly struct ImageGuid : IEquatable<ImageGuid>, IFormattable, IComparable<ImageGuid>, IAs<Guid>, IHashable
{
    /// <summary>
    /// Converts guid into NodeId.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProtoConverter]
    public static ImageGuid FromGuid(Guid id) => new(id);

    /// <summary>
    /// Generates new random Id.
    /// </summary>
    /// <returns></returns>
    public static ImageGuid New() => new(Guid.NewGuid());

    /// <summary>
    /// Converts the string representation of an NodeId to the equivalent NodeId structure.
    /// </summary>
    /// <param name="input">input – The string to convert.</param>
    /// <returns></returns>
    public static ImageGuid Parse(string input) => new(Guid.Parse(input));

    /// <summary>
    /// A read-only instance of the NodeId structure, that can represent default or uninitialized value.
    /// </summary>
    public static readonly ImageGuid Empty = new(Guid.Empty);

    /// <summary>
    /// Converts the string representation of an NodeId to the equivalent NodeId structure.
    /// </summary>
    /// <param name="input">A string containing the NodeId to convert</param>
    /// <param name="imageGuid">An NodeId instance to contain the parsed value. If the method returns true,
    /// result contains a valid NodeId. If the method returns false, result equals Empty.</param>
    /// <returns>true if the parse operation was successful; otherwise, false.</returns>
    public static bool TryParse(string input, [NotNullWhen(true)] out ImageGuid imageGuid)
    {
        if (Guid.TryParse(input, out var guid))
        {
            imageGuid = FromGuid(guid);
            return true;
        }

        imageGuid = Empty;
        return false;
    }

    [ProtoMember(1)] private readonly Guid _id;

    private ImageGuid(Guid id)
    {
        _id = id;
    }

    /// <inheritdoc />
    public Guid Value => _id;

    /// <inheritdoc />
    public bool Equals(ImageGuid other) => _id.Equals(other._id);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ImageGuid other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _id.GetHashCode();

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => _id.ToString(format, formatProvider);

    /// <inheritdoc />
    public override string ToString() => _id.ToString("N");

    /// <inheritdoc />
    public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();

    /// <inheritdoc />
    public int CompareTo(ImageGuid other) => _id.CompareTo(other._id);

    public static bool operator ==(ImageGuid left, ImageGuid right) => left.Equals(right);

    public static bool operator !=(ImageGuid left, ImageGuid right) => !(left == right);

    private class Factory : IFactory<ImageGuid>
    {
        ImageGuid IFactory<ImageGuid>.Empty => Empty;
        ImageGuid IFactory<ImageGuid>.Parse(string value) => ImageGuid.Parse(value);
    }
}
