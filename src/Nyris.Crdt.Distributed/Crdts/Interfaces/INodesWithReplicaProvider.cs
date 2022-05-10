using Nyris.Crdt.Distributed.Model;
using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces;

internal interface INodesWithReplicaProvider
{
    IList<NodeInfo> GetNodesThatShouldHaveReplicaOfCollection(InstanceId instanceId);
}
