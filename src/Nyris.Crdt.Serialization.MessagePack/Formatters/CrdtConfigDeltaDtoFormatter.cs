using MessagePack;
using MessagePack.Formatters;
using Nyris.ManagedCrdtsV2;
using Nyris.ManagedCrdtsV2.Metadata;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class CrdtConfigDeltaDtoFormatter : IMessagePackFormatter<CrdtConfig.DeltaDto>
{
    public void Serialize(ref MessagePackWriter writer, CrdtConfig.DeltaDto value, MessagePackSerializerOptions options)
    {
        switch (value)
        {
            case CrdtConfig.StringDto stringDto:
                writer.Write(true);
                writer.WriteUInt8((byte)stringDto.Field);
                writer.Write(stringDto.Value);
                writer.Write(stringDto.DateTime);
                break;
            case CrdtConfig.UintDto uintDto:
                writer.Write(false);
                writer.WriteUInt8((byte)uintDto.Field);
                writer.Write(uintDto.Value);
                writer.Write(uintDto.DateTime);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public CrdtConfig.DeltaDto Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        switch (reader.ReadBoolean())
        {
            case true:
                var field = (CrdtConfig.ConfigFields)reader.ReadByte();
                var stringValue = reader.ReadString();
                var dateTime = reader.ReadDateTime();
                return new CrdtConfig.StringDto(field, stringValue, dateTime);
            case false:
                field = (CrdtConfig.ConfigFields)reader.ReadByte();
                var uintValue = reader.ReadUInt32();
                dateTime = reader.ReadDateTime();
                return new CrdtConfig.UintDto(field, uintValue, dateTime);
        }
    }
}