using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyris.ManagedCrdtsV2;

[JsonConverter(typeof(ShardIdSystemTextJsonConverter))]
[DebuggerDisplay("{_id}")]
public readonly struct ShardId : IComparable<ShardId>, IEquatable<ShardId>
{
    private readonly uint _id;

    private ShardId(uint id)
    {
        _id = id;
    }

    public uint AsUint => _id;
    public static ShardId FromUint(uint value) => new(value);
    
    public bool Equals(ShardId other) => _id == other._id;
    public override bool Equals(object? obj) => obj is ShardId other && Equals(other);
    public override int GetHashCode() => (int)_id;
    public int CompareTo(ShardId other) => _id.CompareTo(other._id);

    public static bool operator ==(ShardId lhs, ShardId rhs) => lhs.Equals(rhs);
    public static bool operator !=(ShardId lhs, ShardId rhs) => !lhs.Equals(rhs);
    public static bool operator <(ShardId lhs, ShardId rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >(ShardId lhs, ShardId rhs) => lhs.CompareTo(rhs) > 0;
    public static bool operator <=(ShardId lhs, ShardId rhs) => lhs.CompareTo(rhs) <= 0;
    public static bool operator >=(ShardId lhs, ShardId rhs) => lhs.CompareTo(rhs) >= 0;
    
    public class ShardIdSystemTextJsonConverter : JsonConverter<ShardId>
    {
        public override ShardId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) 
            => new(reader.GetUInt32());

        public override void Write(Utf8JsonWriter writer, ShardId value, JsonSerializerOptions options) 
            => writer.WriteNumberValue(value._id);
    }
}