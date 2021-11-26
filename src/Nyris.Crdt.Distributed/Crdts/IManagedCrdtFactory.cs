using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts
{
    public interface IManagedCRDTFactory<out TCRDT, out TImplementation, TRepresentation, TDto>
        where TCRDT : ManagedCRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
    {
        TCRDT Create(WithId<TDto> dto);
    }

    public interface IManagedCRDTFactory<out TCRDT, TRepresentation, TDto> : IManagedCRDTFactory<TCRDT, TCRDT, TRepresentation, TDto>
        where TCRDT : ManagedCRDT<TCRDT, TRepresentation, TDto>
    {
    }
}