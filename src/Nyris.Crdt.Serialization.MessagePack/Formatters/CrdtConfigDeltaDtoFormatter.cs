using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model.Deltas;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class CrdtConfigDeltaDtoFormatter : IMessagePackFormatter<CrdtConfigDelta>
{
    public void Serialize(ref MessagePackWriter writer, CrdtConfigDelta value, MessagePackSerializerOptions options)
    {
        switch (value)
        {
            case CrdtConfigStringDelta stringDto:
                writer.Write(true);
                writer.WriteUInt8((byte)stringDto.Field);
                writer.Write(stringDto.Value);
                writer.Write(stringDto.DateTime);
                break;
            case CrdtConfigUintDelta uintDto:
                writer.Write(false);
                writer.WriteUInt8((byte)uintDto.Field);
                writer.Write(uintDto.Value);
                writer.Write(uintDto.DateTime);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public CrdtConfigDelta Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        switch (reader.ReadBoolean())
        {
            case true:
                var field = (ConfigFields)reader.ReadByte();
                var stringValue = reader.ReadString();
                var dateTime = reader.ReadDateTime();
                return new CrdtConfigStringDelta(field, stringValue, dateTime);
            case false:
                field = (ConfigFields)reader.ReadByte();
                var uintValue = reader.ReadUInt32();
                dateTime = reader.ReadDateTime();
                return new CrdtConfigUintDelta(field, uintValue, dateTime);
        }
    }
}