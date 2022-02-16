namespace Nyris.Crdt
{
    public interface ICRDTFactory<out TCRDT, in TDto>
        where TCRDT : ICRDT<TDto>
    {
        TCRDT Create(TDto dto);
    }
}