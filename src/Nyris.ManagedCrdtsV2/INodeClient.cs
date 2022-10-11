using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface INodeClient
{
    Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas, CancellationToken cancellationToken = default);
    Task MergeMetadataAsync(MetadataDto kind, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    IDuplexMetadataDeltasStream GetMetadataDuplexStream();
    IDuplexDeltasStream GetDeltaDuplexStream();
    Task<ReadOnlyMemory<byte>> JoinToClusterAsync(NodeInfo nodeInfo, CancellationToken cancellationToken = default);
}