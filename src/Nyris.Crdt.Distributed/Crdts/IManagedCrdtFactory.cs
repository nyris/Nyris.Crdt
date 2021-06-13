namespace Nyris.Crdt.Distributed.Crdts
{
    public interface IManagedCRDTFactory<out TCRDT, out TImplementation, TRepresentation, in TDto>
        where TCRDT : ManagedCRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
    {
        TCRDT Create(TDto dto);
    }

    public interface IManagedCRDTFactory<out TCRDT, TRepresentation, in TDto> : IManagedCRDTFactory<TCRDT, TCRDT, TRepresentation, TDto>
        where TCRDT : ManagedCRDT<TCRDT, TRepresentation, TDto>
    {
    }
}