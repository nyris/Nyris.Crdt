using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract]
    public sealed class WithId<TDto>
    {
        [ProtoMember(1)]
        public string Id { get; init; } = string.Empty;

        [ProtoMember(2)]
        public TDto? Dto { get; init; }
    }
}