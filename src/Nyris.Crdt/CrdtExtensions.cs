using System.Threading.Tasks;

namespace Nyris.Crdt
{
    public static class CrdtExtensions
    {
        public static MergeResult MaybeMerge<TDto>(this ICRDT<TDto> crdt, TDto? other)
            => ReferenceEquals(other, null) ? MergeResult.NotUpdated : crdt.Merge(other);

        public static Task<MergeResult> MaybeMergeAsync<TDto>(this IAsyncCRDT<TDto> crdt, TDto? other)
            => ReferenceEquals(other, null) ? Task.FromResult(MergeResult.NotUpdated) : crdt.MergeAsync(other);
    }
}