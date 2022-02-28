using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    public interface ISelectShards
    {
        IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds);
    }
}