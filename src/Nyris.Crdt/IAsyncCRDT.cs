using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nyris.Crdt
{
    public interface IAsyncDtoBatchProvider<TDto>
    {
        IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync();
    }

    public interface IAsyncCRDT<in TImplementation, out TRepresentation, TDto>
        : IAsyncCRDT<TImplementation, TRepresentation>, IAsyncDtoBatchProvider<TDto>
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
    {
        Task<TDto> ToDtoAsync();
    }

    public interface IAsyncCRDT<in TImplementation, out TRepresentation>
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation>
    {
        TRepresentation Value { get; }

        Task<MergeResult> MergeAsync(TImplementation other);
    }
}