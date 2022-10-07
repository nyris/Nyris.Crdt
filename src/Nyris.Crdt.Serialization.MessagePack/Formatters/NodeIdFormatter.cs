using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class NodeIdFormatter : IMessagePackFormatter<NodeId>
{
    public static readonly NodeIdFormatter Instance = new();
    
    public void Serialize(ref MessagePackWriter writer, NodeId value, MessagePackSerializerOptions options)
    {
        writer.Write(value.AsReadOnlySpan);
    }

    public NodeId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) 
        => NodeId.FromChars(reader.ReadString());
}