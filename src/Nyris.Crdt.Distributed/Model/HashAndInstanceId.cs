using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    // TODO: Fix this
    [ProtoContract(SkipConstructor = true)]
#pragma warning disable CA1819 // Properties should not return arrays
    public sealed record HashAndInstanceId(
        [property: ProtoMember(1)] byte[] Hash,
#pragma warning restore CA1819 // Properties should not return arrays
        [property: ProtoMember(2)] InstanceId InstanceId
    );
}
