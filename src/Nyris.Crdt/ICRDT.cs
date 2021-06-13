namespace Nyris.Crdt
{
    public interface ICRDT<in TImplementation, out TRepresentation>
        where TImplementation : ICRDT<TImplementation, TRepresentation>
    {
        TRepresentation Value { get; }

        MergeResult Merge(TImplementation other);
    }

    public interface ICRDT<in TImplementation, out TRepresentation, out TDto> : ICRDT<TImplementation, TRepresentation>
        where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
    {
        TDto ToDto();
    }
}
