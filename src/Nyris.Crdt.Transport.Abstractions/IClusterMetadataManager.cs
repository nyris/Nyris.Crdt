using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IClusterMetadataManager
{
    IAsyncEnumerable<(MetadataDto, ReadOnlyMemory<byte>)> AddNewNodeAsync(NodeInfo nodeInfo, OperationContext context);
    Task MergeAsync(MetadataDto kind, ReadOnlyMemory<byte> dto, OperationContext context);
    Task<ImmutableArray<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken);
    ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> GetCausalTimestamps(CancellationToken cancellationToken);
    IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltasAsync(MetadataDto kind, ReadOnlyMemory<byte> timestamp,
        CancellationToken cancellationToken);
    Task ReportSyncSuccessfulAsync(InstanceId instanceId, ShardId shardId, OperationContext context);
}