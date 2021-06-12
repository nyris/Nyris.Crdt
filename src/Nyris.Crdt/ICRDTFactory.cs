namespace Nyris.Crdt
{
    public interface ICRDTFactory<out TCRDT, out TImplementation, TRepresentation, in TDto>
        where TCRDT : ICRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
    {
        TCRDT Create(TDto dto);
    }

    public interface ICRDTFactory<out TCRDT, TRepresentation, in TDto> : ICRDTFactory<TCRDT, TCRDT, TRepresentation, TDto>
        where TCRDT : ICRDT<TCRDT, TRepresentation, TDto>
    {
    }
}