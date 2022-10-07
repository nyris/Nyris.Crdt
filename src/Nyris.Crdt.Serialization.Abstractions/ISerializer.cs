namespace Nyris.Crdt.Serialization.Abstractions;

public interface ISerializer
{
    T Deserialize<T>(ReadOnlyMemory<byte> bytes);
    ReadOnlyMemory<byte> Serialize<T>(T value);
}
