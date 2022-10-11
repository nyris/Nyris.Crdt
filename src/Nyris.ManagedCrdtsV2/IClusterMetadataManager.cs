using System.Collections.Immutable;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IClusterMetadataManager
{
    Task<ReadOnlyMemory<byte>> AddNewNodeAsync(NodeInfo nodeInfo, CancellationToken cancellationToken = default);
    Task MergeAsync(MetadataDto kind, ReadOnlyMemory<byte> dto, CancellationToken cancellationToken = default);
    Task<ImmutableArray<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken);
    ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> GetCausalTimestamps(CancellationToken cancellationToken);
    IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltasAsync(MetadataDto kind, ReadOnlyMemory<byte> timestamp,
        CancellationToken cancellationToken);
}