namespace Nyris.Crdt
{
    // ReSharper disable once InconsistentNaming
    public interface ICRDT<in TImplementation, out TRepresentation>
        where TImplementation : ICRDT<TImplementation, TRepresentation>
    {
        public TRepresentation Value { get; }

        public MergeResult Merge(TImplementation other);
    }

    // ReSharper disable once InconsistentNaming
    public interface ICRDT<in TImplementation, out TRepresentation, out TDto> : ICRDT<TImplementation, TRepresentation>
        where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
    {
        public TDto ToDto();
    }
}
