using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Interfaces;

public interface IAsyncCRDT<TDto>
{
    Task<MergeResult> MergeAsync(TDto other, CancellationToken cancellationToken = default);
    Task<TDto> ToDtoAsync(CancellationToken cancellationToken = default);
}
