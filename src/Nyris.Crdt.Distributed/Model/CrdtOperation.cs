using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record CrdtOperation<TOperation>([property: ProtoMember(1)] string TypeName,
        [property: ProtoMember(2)] InstanceId InstanceId,
        [property: ProtoMember(3)] string TraceId,
        [property: ProtoMember(4)] ShardId ShardId,
        [property: ProtoMember(5)] TOperation Operation);
}