using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record FindIdsOperation([property: ProtoMember(1)] string ImageId) : RegistryOperation
    {
        /// <inheritdoc />
        public override IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds) => shardIds;
    }
}