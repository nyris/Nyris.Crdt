using System.Collections.Generic;
using System.Threading;

namespace Nyris.Crdt.Interfaces;

public interface IAsyncDtoBatchProvider<out TDto>
{
    IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default);
}
