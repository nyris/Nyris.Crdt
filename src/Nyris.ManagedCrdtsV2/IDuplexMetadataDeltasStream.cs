using System.Collections.Immutable;

namespace Nyris.ManagedCrdtsV2;

public interface IDuplexMetadataDeltasStream : IDisposable
{
    Task<ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>>> ExchangeMetadataTimestampsAsync(
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps, CancellationToken cancellationToken);

    Task SendDeltasAsync(MetadataDto kind, IAsyncEnumerable<ReadOnlyMemory<byte>> deltas,
        CancellationToken cancellationToken);

    IAsyncEnumerable<(MetadataDto kind, ReadOnlyMemory<byte>)> GetDeltasAsync(CancellationToken cancellationToken);

    Task FinishSendingAsync();
}