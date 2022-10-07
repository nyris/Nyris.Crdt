using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Sets;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class ObservedRemoveSetDeltaDtoFormatter<TActorId, TValue> 
    : IMessagePackFormatter<OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDto> 
    where TActorId : IEquatable<TActorId>, IComparable<TActorId> 
    where TValue : IEquatable<TValue>
{
    private const byte AdditionDelta = 1;
    private const byte RemovalDelta = 2;
    private const byte RemovalRangeDelta = 3;
    
    public void Serialize(ref MessagePackWriter writer, OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDto value, MessagePackSerializerOptions options)
    {
        var actorFormatter = options.Resolver.GetFormatterWithVerify<TActorId>();
        switch (value)
        {
            case OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDtoAddition (var val, var actorId, var version):
                writer.WriteUInt8(AdditionDelta);
                var valueFormatter = options.Resolver.GetFormatterWithVerify<TValue>();
                valueFormatter.Serialize(ref writer, val, options);
                actorFormatter.Serialize(ref writer, actorId, options);
                writer.WriteUInt64(version);
                break;
            case OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDtoDeletedDot (var actorId, var version):
                writer.WriteUInt8(RemovalDelta);
                actorFormatter.Serialize(ref writer, actorId, options);
                writer.WriteUInt64(version);
                break;
            case OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDtoDeletedRange (var actorId, var range):
                writer.WriteUInt8(RemovalDelta);
                actorFormatter.Serialize(ref writer, actorId, options);
                writer.WriteUInt64(range.From);
                writer.WriteUInt64(range.To);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDto Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var actorFormatter = options.Resolver.GetFormatterWithVerify<TActorId>();
        var typeByte = reader.ReadByte();
        switch (typeByte)
        {
            case AdditionDelta:
                var valueFormatter = options.Resolver.GetFormatterWithVerify<TValue>();
                var value = valueFormatter.Deserialize(ref reader, options);
                var actorId = actorFormatter.Deserialize(ref reader, options);
                var version = reader.ReadUInt64();
                return OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDto.Added(value, actorId, version);
            case RemovalDelta:
                actorId = actorFormatter.Deserialize(ref reader, options);
                version = reader.ReadUInt64();
                return OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDto.Removed(actorId, version);
            case RemovalRangeDelta:
                actorId = actorFormatter.Deserialize(ref reader, options);
                var range = new Range(reader.ReadUInt64(), reader.ReadUInt64());
                return OptimizedObservedRemoveSetV2<TActorId, TValue>.DeltaDto.Removed(actorId, range);
            default:
                throw new ArgumentOutOfRangeException(nameof(typeByte), 
                    $"Type byte of a serialized deltaDto has an unknown value of {typeByte}");
        }
    }
}