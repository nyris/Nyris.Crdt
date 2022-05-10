using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public record TypeNameAndInstanceId(
        [property: ProtoMember(1)] string TypeName,
        [property: ProtoMember(2)] InstanceId InstanceId
    );
}
