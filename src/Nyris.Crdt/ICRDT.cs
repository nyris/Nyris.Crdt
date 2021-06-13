using System.Collections.Generic;

namespace Nyris.Crdt
{
    public interface ICRDT<in TImplementation, out TRepresentation>
        where TImplementation : ICRDT<TImplementation, TRepresentation>
    {
        TRepresentation Value { get; }

        MergeResult MergeAsync(TImplementation other);
    }

    public interface ICRDT<in TImplementation, out TRepresentation, out TDto> : ICRDT<TImplementation, TRepresentation>
        where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
    {
        TDto ToDto();
    }
}
