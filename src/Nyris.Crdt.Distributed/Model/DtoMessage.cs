using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record DtoMessage<TDto>([property: ProtoMember(3)] string CrdtTypeName,
            [property: ProtoMember(1)] string Id,
            [property: ProtoMember(2)] TDto Value);
}