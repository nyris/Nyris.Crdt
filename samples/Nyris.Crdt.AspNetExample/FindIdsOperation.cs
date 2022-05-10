using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf;
using System.Collections.Generic;

namespace Nyris.Crdt.AspNetExample;

[ProtoContract(SkipConstructor = true)]
public sealed record FindIdsOperation([property: ProtoMember(1)] string ImageId) : RegistryOperation
{
    [ProtoIgnore]
    public override uint PropagateToNodes { get; init; } = 0;

    /// <inheritdoc />
    public override IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds) => shardIds;
}
