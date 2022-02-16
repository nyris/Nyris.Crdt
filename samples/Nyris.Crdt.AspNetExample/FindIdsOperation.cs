using Nyris.Crdt.Distributed.Crdts.Operations;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record FindIdsOperation([property: ProtoMember(1)] string ImageId) : RegistryOperation;
}