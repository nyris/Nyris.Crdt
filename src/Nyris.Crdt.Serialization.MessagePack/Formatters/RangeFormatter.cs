using MessagePack;
using MessagePack.Formatters;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class RangeFormatter : IMessagePackFormatter<Range>
{
    public void Serialize(ref MessagePackWriter writer, Range value, MessagePackSerializerOptions options)
    {
        writer.WriteUInt64(value.From);
        writer.WriteUInt64(value.To);
    }

    public Range Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
        new(reader.ReadUInt64(), reader.ReadUInt64());
}