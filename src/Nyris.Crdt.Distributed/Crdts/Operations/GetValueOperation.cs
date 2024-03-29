using Nyris.Crdt.Interfaces;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations;

[ProtoContract(SkipConstructor = true)]
public record GetValueOperation<TKey>([property: ProtoMember(1)] TKey Key) : OperationWithKey<TKey>
    where TKey : IHashable
{
    [ProtoIgnore]
    public override uint PropagateToNodes { get; init; } = 0;
}
