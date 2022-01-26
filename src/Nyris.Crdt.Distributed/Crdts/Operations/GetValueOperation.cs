using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    [ProtoContract(SkipConstructor = true)]
    public record GetValueOperation<TKey>([property: ProtoMember(1)] TKey Key) : RegistryOperation;
}