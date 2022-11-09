using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IDuplexMetadataDeltasStream : IDisposable
{
    Task<ImmutableDictionary<MetadataKind, ReadOnlyMemory<byte>>> ExchangeMetadataTimestampsAsync(
        ImmutableDictionary<MetadataKind, ReadOnlyMemory<byte>> timestamps, OperationContext context);

    Task SendDeltasAsync(MetadataKind kind, IAsyncEnumerable<ReadOnlyMemory<byte>> deltas,
        CancellationToken cancellationToken);

    IAsyncEnumerable<(MetadataKind kind, ReadOnlyMemory<byte>)> GetDeltasAsync(CancellationToken cancellationToken);

    Task FinishSendingAsync();
}