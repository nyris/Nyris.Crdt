using System.Collections.Generic;
using System.Threading;

namespace Nyris.Crdt
{
    public interface IAsyncDtoBatchProvider<TDto>
    {
        IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default);
    }
}