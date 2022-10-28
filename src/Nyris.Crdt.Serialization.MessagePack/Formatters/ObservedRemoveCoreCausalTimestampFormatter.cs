using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class ObservedRemoveCoreCausalTimestampFormatter<TActorId, TValue> 
    : IMessagePackFormatter<ObservedRemoveCore<TActorId, TValue>.CausalTimestamp> 
    where TActorId : IEquatable<TActorId>, IComparable<TActorId> 
    where TValue : IEquatable<TValue>
{
    public void Serialize(ref MessagePackWriter writer, ObservedRemoveCore<TActorId, TValue>.CausalTimestamp value, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        formatter.Serialize(ref writer, value.Since, options);
    }

    public ObservedRemoveCore<TActorId, TValue>.CausalTimestamp Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        var since = formatter.Deserialize(ref reader, options);
        return new ObservedRemoveCore<TActorId, TValue>.CausalTimestamp(since);
    }
}