using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    public interface IManagedCRDTFactory<out TCRDT, in TDto>
        where TCRDT : ManagedCRDT<TDto>
    {
        TCRDT Create(InstanceId instanceId);
    }
}