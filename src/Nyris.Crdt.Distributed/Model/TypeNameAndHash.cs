using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    // TODO: Fix this
    [ProtoContract(SkipConstructor = true)]
    public sealed record TypeNameAndHash(
        [property: ProtoMember(1)] string TypeName,
#pragma warning disable CA1819 // Properties should not return arrays
        [property: ProtoMember(2)] byte[] Hash
    );
#pragma warning restore CA1819 // Properties should not return arrays
}
