using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class NodeIdFormatter : IMessagePackFormatter<NodeId>
{
    public static readonly NodeIdFormatter Instance = new();
    
    public void Serialize(ref MessagePackWriter writer, NodeId value, MessagePackSerializerOptions options)
    {
        writer.Write(value.ToString());
    }

    public NodeId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) 
        => NodeId.FromString(reader.ReadString());
}