using MessagePack;
using MessagePack.Formatters;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExampleV2;

public class ImageIdFormatter : IMessagePackFormatter<ImageId>
{
    public void Serialize(ref MessagePackWriter writer, ImageId value, MessagePackSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            writer.WriteNil();
            return;
        }
        
        GuidFormatter.Instance.Serialize(ref writer, value.AsGuid, options);
    }

    public ImageId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.TryReadNil()) return ImageId.Empty;
        
        var guid = GuidFormatter.Instance.Deserialize(ref reader, options);
        return new ImageId(guid);
    }
}
