using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class InstanceIdFormatter : IMessagePackFormatter<InstanceId>
{
    public void Serialize(ref MessagePackWriter writer, InstanceId value, MessagePackSerializerOptions options) 
        => writer.Write(value.AsReadOnlySpan);

    public InstanceId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) 
        => InstanceId.FromChars(reader.ReadString());
}