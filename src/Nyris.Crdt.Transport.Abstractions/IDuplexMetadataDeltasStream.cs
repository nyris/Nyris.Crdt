using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IDuplexMetadataDeltasStream : IDisposable
{
    Task<ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>>> ExchangeMetadataTimestampsAsync(
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps, OperationContext context);

    Task SendDeltasAsync(MetadataDto kind, IAsyncEnumerable<ReadOnlyMemory<byte>> deltas,
        CancellationToken cancellationToken);

    IAsyncEnumerable<(MetadataDto kind, ReadOnlyMemory<byte>)> GetDeltasAsync(CancellationToken cancellationToken);

    Task FinishSendingAsync();
}