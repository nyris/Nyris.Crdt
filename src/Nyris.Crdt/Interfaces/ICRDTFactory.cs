namespace Nyris.Crdt.Interfaces;

public interface ICRDTFactory<out TCRDT, in TDto>
    where TCRDT : ICRDT<TDto>
{
    TCRDT Create(TDto dto);
}
