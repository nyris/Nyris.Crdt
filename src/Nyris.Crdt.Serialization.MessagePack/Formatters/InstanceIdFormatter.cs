using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class InstanceIdFormatter : IMessagePackFormatter<InstanceId>
{
    public void Serialize(ref MessagePackWriter writer, InstanceId value, MessagePackSerializerOptions options)
        => writer.Write(value.ToString());

    public InstanceId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => new(reader.ReadString());
}