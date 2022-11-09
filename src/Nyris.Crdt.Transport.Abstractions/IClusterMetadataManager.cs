using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IClusterMetadataManager
{
    IAsyncEnumerable<(MetadataKind, ReadOnlyMemory<byte>)> AddNewNodeAsync(NodeInfo nodeInfo, OperationContext context);
    Task MergeAsync(MetadataKind kind, ReadOnlyMemory<byte> dto, OperationContext context);
    ImmutableDictionary<MetadataKind, ReadOnlyMemory<byte>> GetCausalTimestamps(CancellationToken cancellationToken);
    IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltasAsync(MetadataKind kind, ReadOnlyMemory<byte> timestamp,
        CancellationToken cancellationToken);
}