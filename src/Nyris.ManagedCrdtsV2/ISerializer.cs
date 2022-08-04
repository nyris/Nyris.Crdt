namespace Nyris.ManagedCrdtsV2;

public interface ISerializer
{
    T Deserialize<T>(ReadOnlySpan<byte> bytes);
    ReadOnlyMemory<byte> Serialize<T>(T value);
}