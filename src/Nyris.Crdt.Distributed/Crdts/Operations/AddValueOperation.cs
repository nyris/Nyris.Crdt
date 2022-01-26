using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    [ProtoContract(SkipConstructor = true)]
    public record AddValueOperation<TKey, TValue, TTimeStamp>([property: ProtoMember(1)] TKey Key,
            [property: ProtoMember(2)] TValue Value,
            [property: ProtoMember(3)] TTimeStamp TimeStamp) : RegistryOperation;
}