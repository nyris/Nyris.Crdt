using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface INodeClient
{
    Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas, OperationContext context);
    Task MergeMetadataAsync(MetadataDto kind, ReadOnlyMemory<byte> data, OperationContext context);
    IDuplexMetadataDeltasStream GetMetadataDuplexStream();
    IDuplexDeltasStream GetDeltaDuplexStream();
    IAsyncEnumerable<(MetadataDto, ReadOnlyMemory<byte>)> JoinToClusterAsync(NodeInfo nodeInfo,
        OperationContext context);
}