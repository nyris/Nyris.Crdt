using Nyris.Crdt.Distributed.Crdts.Abstractions;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    public interface IManagedCRDTFactory<out TCRDT, TRepresentation, in TDto>
        where TCRDT : ManagedCRDT<TCRDT, TRepresentation, TDto>
    {
        TCRDT Create(TDto dto, string instanceId);
        TCRDT Create(string instanceId);
    }
}