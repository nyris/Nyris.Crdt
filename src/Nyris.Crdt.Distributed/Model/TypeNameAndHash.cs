using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record TypeNameAndHash([property: ProtoMember(1)] string TypeName,
        [property: ProtoMember(2)] byte[] Hash);
}