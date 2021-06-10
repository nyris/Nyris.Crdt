namespace Nyris.Crdt
{
    public interface ICRDTFactory<T, TRepresentation, TDto>
        where T : ICRDT<T, TRepresentation, TDto>
    {
        T Create(TDto dto);
    }
}