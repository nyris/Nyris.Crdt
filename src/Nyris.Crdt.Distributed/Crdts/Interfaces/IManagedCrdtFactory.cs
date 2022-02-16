using Nyris.Crdt.Distributed.Crdts.Abstractions;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    public interface IManagedCRDTFactory<out TCRDT, in TDto>
        where TCRDT : ManagedCRDT<TDto>
    {
        TCRDT Create(string instanceId);
    }
}