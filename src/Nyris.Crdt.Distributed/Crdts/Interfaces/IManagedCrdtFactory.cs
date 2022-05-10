using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces;

public interface IManagedCRDTFactory<out TCrdt, in TDto>
    where TCrdt : ManagedCRDT<TDto>
{
    TCrdt Create(InstanceId instanceId);
}

public interface INodeAwareManagedCrdtFactory<out TCrdt, in TDto>
    where TCrdt : ManagedCRDT<TDto>
{
    TCrdt Create(InstanceId instanceId, NodeInfo nodeInfo);
}
