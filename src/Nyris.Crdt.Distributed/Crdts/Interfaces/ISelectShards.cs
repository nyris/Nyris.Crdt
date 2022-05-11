using Nyris.Crdt.Distributed.Model;
using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces;

public interface ISelectShards
{
    IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds);
}
