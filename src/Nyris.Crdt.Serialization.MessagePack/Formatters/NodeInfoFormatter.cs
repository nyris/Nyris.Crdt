using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class NodeInfoFormatter : IMessagePackFormatter<NodeInfo>
{
    public void Serialize(ref MessagePackWriter writer, NodeInfo value, MessagePackSerializerOptions options)
    {
        UriFormatter.Instance.Serialize(ref writer, value.Address, options);
        NodeIdFormatter.Instance.Serialize(ref writer, value.Id, options);
    }

    public NodeInfo Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
        new(UriFormatter.Instance.Deserialize(ref reader, options),
            NodeIdFormatter.Instance.Deserialize(ref reader, options));
}