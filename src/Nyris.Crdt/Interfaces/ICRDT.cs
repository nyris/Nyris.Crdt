namespace Nyris.Crdt.Interfaces;

public interface ICRDT<TDto>
{
    MergeResult Merge(TDto other);
    TDto ToDto();
}
