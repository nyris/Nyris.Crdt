using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public record WithId<T>([property: ProtoMember(1)] string Id, [property: ProtoMember(2)] T? Value);
}