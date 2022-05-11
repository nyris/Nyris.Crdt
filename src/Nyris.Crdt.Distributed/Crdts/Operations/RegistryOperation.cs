using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Crdts.Operations;

public abstract record RegistryOperation : Operation, ISelectShards
{
    public abstract uint PropagateToNodes { get; init; }

    /// <inheritdoc />
    public abstract IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds);
}
