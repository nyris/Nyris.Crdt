using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record TypeNameAndInstanceId([property: ProtoMember(1)] string TypeName, [property: ProtoMember(2)] string InstanceId)
    {
        public void Deconstruct(out string typeName, out string instanceId)
        {
            typeName = TypeName;
            instanceId = InstanceId;
        }
    }
}