using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    public abstract record RegistryOperation : Operation, ISelectShards
    {
        /// <inheritdoc />
        public abstract IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds);
    }
}