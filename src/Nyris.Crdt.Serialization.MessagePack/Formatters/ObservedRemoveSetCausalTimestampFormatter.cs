using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class ObservedRemoveSetCausalTimestampFormatter<TActorId, TValue> 
    : IMessagePackFormatter<ObservedRemoveDtos<TActorId, TValue>.CausalTimestamp> 
    where TActorId : IEquatable<TActorId>, IComparable<TActorId> 
    where TValue : IEquatable<TValue>
{
    public void Serialize(ref MessagePackWriter writer, ObservedRemoveDtos<TActorId, TValue>.CausalTimestamp value, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        formatter.Serialize(ref writer, value.Since, options);
    }

    public ObservedRemoveDtos<TActorId, TValue>.CausalTimestamp Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        var since = formatter.Deserialize(ref reader, options);
        return new ObservedRemoveDtos<TActorId, TValue>.CausalTimestamp(since);
    }
}