using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public enum MetadataDto 
{
    NodeSet = 1,
    NodeSetFull = 2,
    CrdtInfos = 3,
    CrdtConfigs = 4
}

public interface INodeClient
{
    Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas, CancellationToken cancellationToken = default);
    Task MergeMetadataAsync(MetadataDto kind, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    Task<ReadOnlyMemory<byte>> JoinToClusterAsync(NodeInfo nodeInfo, CancellationToken cancellationToken = default);
}