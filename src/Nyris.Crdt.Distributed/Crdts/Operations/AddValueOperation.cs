using Nyris.Crdt.Distributed.Crdts.Interfaces;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    [ProtoContract(SkipConstructor = true)]
    public record AddValueOperation<TKey, TValue, TTimeStamp>([property: ProtoMember(1)] TKey Key,
        [property: ProtoMember(2)] TValue Value,
        [property: ProtoMember(3)] TTimeStamp TimeStamp,
        [property: ProtoMember(4)] uint PropagateToNodes) : OperationWithKey<TKey>
        where TKey : IHashable;
}