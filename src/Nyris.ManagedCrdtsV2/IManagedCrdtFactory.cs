using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IManagedCrdtFactory
{
    TCrdt Create<TCrdt>(InstanceId instanceId, IReplicaDistributor replicaDistributor);
    ManagedCrdt Create(string typeName, InstanceId instanceId, IReplicaDistributor replicaDistributor);
}