using Nyris.Crdt.Managed.Model;
using Nyris.ManagedCrdtsV2.ManagedCrdts;

namespace Nyris.ManagedCrdtsV2;

public interface IManagedCrdtFactory
{
    TCrdt Create<TCrdt>(InstanceId instanceId, IReplicaDistributor replicaDistributor);
    ManagedCrdt Create(string typeName, InstanceId instanceId, IReplicaDistributor replicaDistributor);
}