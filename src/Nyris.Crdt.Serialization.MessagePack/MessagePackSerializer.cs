using MessagePack;
using MessagePack.Resolvers;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.Crdt.Serialization.MessagePack;

public sealed class MessagePackSerializer : ISerializer
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(CustomResolver.Instance, StandardResolver.Instance));

    public T Deserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        return global::MessagePack.MessagePackSerializer.Deserialize<T>(bytes, Options);
    }

    public ReadOnlyMemory<byte> Serialize<T>(T value)
    {
        return global::MessagePack.MessagePackSerializer.Serialize(value, Options);
    }
}