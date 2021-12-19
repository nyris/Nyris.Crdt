using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    internal interface INodesWithReplicaProvider
    {
        IList<NodeInfo> GetNodesThatShouldHaveReplicaOfCollection(string instanceId);
    }
}