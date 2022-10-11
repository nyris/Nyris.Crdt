using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class ObservedRemoveMapCausalTimestampFormatter<TActorId, TKey, TValue, TValueDelta, TValueTimestamp> 
    : IMessagePackFormatter<ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.CausalTimestamp>
    where TActorId : IEquatable<TActorId>, IComparable<TActorId> 
    where TValue : class, IDeltaCrdt<TValueDelta, TValueTimestamp>, new()
    where TKey : IEquatable<TKey>
{

    public void Serialize(ref MessagePackWriter writer, ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.CausalTimestamp value, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        formatter.Serialize(ref writer, value.Since, options);
    }

    public ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.CausalTimestamp Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        var since = formatter.Deserialize(ref reader, options);
        return new ObservedRemoveMap<TActorId, TKey, TValue, TValueDelta, TValueTimestamp>.CausalTimestamp(since);
    }
}