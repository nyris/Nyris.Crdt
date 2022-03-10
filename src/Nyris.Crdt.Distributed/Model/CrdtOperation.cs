using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record CrdtOperation<TOperation>([property: ProtoMember(1)] string TypeName,
        [property: ProtoMember(2)] string InstanceId,
        [property: ProtoMember(3)] string TraceId,
		[property: ProtoMember(4)] int PropagateToNodes,
        [property: ProtoMember(5)] ShardId ShardId,
        [property: ProtoMember(6)] TOperation Operation);
}