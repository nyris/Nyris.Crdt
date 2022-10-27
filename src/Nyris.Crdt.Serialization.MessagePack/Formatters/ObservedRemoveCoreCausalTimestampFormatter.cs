using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class ObservedRemoveCoreCausalTimestampFormatter<TActorId, TValue> 
    : IMessagePackFormatter<OptimizedObservedRemoveCore<TActorId, TValue>.CausalTimestamp> 
    where TActorId : IEquatable<TActorId>, IComparable<TActorId> 
    where TValue : IEquatable<TValue>
{
    public void Serialize(ref MessagePackWriter writer, OptimizedObservedRemoveCore<TActorId, TValue>.CausalTimestamp value, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        formatter.Serialize(ref writer, value.Since, options);
    }

    public OptimizedObservedRemoveCore<TActorId, TValue>.CausalTimestamp Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var formatter = options.Resolver.GetFormatterWithVerify<ImmutableDictionary<TActorId, ImmutableArray<Model.Range>>>();
        var since = formatter.Deserialize(ref reader, options);
        return new OptimizedObservedRemoveCore<TActorId, TValue>.CausalTimestamp(since);
    }
}