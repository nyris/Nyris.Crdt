using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.ManagedCrdts.Factory;

internal interface IManagedCrdtFactory
{
    TCrdt Create<TCrdt>(InstanceId instanceId, IReplicaDistributor replicaDistributor);
    ManagedCrdt Create(string typeName, InstanceId instanceId, IReplicaDistributor replicaDistributor);
}