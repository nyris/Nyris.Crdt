using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record HashAndInstanceId([property: ProtoMember(1)] byte[] Hash,
        [property: ProtoMember(2)] InstanceId InstanceId);
}