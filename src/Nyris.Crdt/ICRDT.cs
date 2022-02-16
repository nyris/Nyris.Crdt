namespace Nyris.Crdt
{
    public interface ICRDT<TDto>
    {
        MergeResult Merge(TDto other);
        TDto ToDto();
    }
}
