using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Interfaces;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class ObservedRemoveMapDeltaDtoFormatter<TActorId, TKey, TValue, TValueDelta, TValueTimestamp> 
    : IMessagePackFormatter<ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDto> 
    where TActorId : IEquatable<TActorId>, IComparable<TActorId> 
    where TValue : class, IDeltaCrdt<TValueDelta, TValueTimestamp>, new()
    where TKey : IEquatable<TKey>
{
    private const byte AdditionDelta = 1;
    private const byte RemovalDelta = 2;
    private const byte RemovalRangeDelta = 3;

    public void Serialize(ref MessagePackWriter writer, ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDto value, MessagePackSerializerOptions options)
    {
        var actorFormatter = options.Resolver.GetFormatterWithVerify<TActorId>();
        switch (value)
        {
            case ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDtoAddition (var actorId, var version, var key, var valueDeltas):
                writer.WriteUInt8(AdditionDelta);

                actorFormatter.Serialize(ref writer, actorId, options);
                writer.WriteUInt64(version);
                
                var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
                keyFormatter.Serialize(ref writer, key, options);
                
                var deltasFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TValueDelta>>();
                deltasFormatter.Serialize(ref writer, valueDeltas, options);
                break;
            case ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDtoDeletedDot (var actorId, var version):
                writer.WriteUInt8(RemovalDelta);
                actorFormatter.Serialize(ref writer, actorId, options);
                writer.WriteUInt64(version);
                break;
            case ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDtoDeletedRange (var actorId, var range):
                writer.WriteUInt8(RemovalDelta);
                actorFormatter.Serialize(ref writer, actorId, options);
                writer.WriteUInt64(range.From);
                writer.WriteUInt64(range.To);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDto Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    { 
        var actorFormatter = options.Resolver.GetFormatterWithVerify<TActorId>();
        var typeByte = reader.ReadByte();
        switch (typeByte)
        {
            case AdditionDelta:
                var actorId = actorFormatter.Deserialize(ref reader, options);
                var version = reader.ReadUInt64();
                
                var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
                var key = keyFormatter.Deserialize(ref reader, options);
                
                var deltasFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TValueDelta>>();
                var valueDeltas = deltasFormatter.Deserialize(ref reader, options);
                return ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDto.Added(actorId, version, key, valueDeltas);
            case RemovalDelta:
                actorId = actorFormatter.Deserialize(ref reader, options);
                version = reader.ReadUInt64();
                return ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDto.Removed(actorId, version);
            case RemovalRangeDelta:
                actorId = actorFormatter.Deserialize(ref reader, options);
                var range = new Range(reader.ReadUInt64(), reader.ReadUInt64());
                return ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.DeltaDto.Removed(actorId, range);
            default:
                throw new ArgumentOutOfRangeException(nameof(typeByte), 
                    $"Type byte of a serialized deltaDto has an unknown value of {typeByte}");
        }
    }
}