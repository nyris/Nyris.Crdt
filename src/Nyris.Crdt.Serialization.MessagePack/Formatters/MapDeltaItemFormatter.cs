using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class MapDeltaItemFormatter<TActorId, TKey, TValue, TValueDto, TValueTimestamp> : IMessagePackFormatter<
    ObservedRemoveMapV2<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.MapDeltaItem>
    where TActorId : IEquatable<TActorId>
    where TKey : IEquatable<TKey>
    where TValue : class, IDeltaCrdt<TValueDto, TValueTimestamp>, new()
{
    public void Serialize(ref MessagePackWriter writer,
        ObservedRemoveMapV2<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.MapDeltaItem value,
        MessagePackSerializerOptions options)
    {
        var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
        keyFormatter.Serialize(ref writer, value.Key, options);

        var deltasFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TValueDto>>();
        deltasFormatter.Serialize(ref writer, value.ValueDeltas, options);
    }

    public ObservedRemoveMapV2<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.MapDeltaItem Deserialize(ref MessagePackReader reader,
        MessagePackSerializerOptions options)
    {
        var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
        var key = keyFormatter.Deserialize(ref reader, options);

        var deltasFormatter = options.Resolver.GetFormatterWithVerify<ImmutableArray<TValueDto>>();
        var valueDeltas = deltasFormatter.Deserialize(ref reader, options);
        return new ObservedRemoveMapV2<TActorId, TKey, TValue, TValueDto, TValueTimestamp>.MapDeltaItem(key, valueDeltas);
    }
}