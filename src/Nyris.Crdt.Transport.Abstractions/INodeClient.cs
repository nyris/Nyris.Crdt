using Nyris.Crdt.Managed.Model;
using InstanceId = Nyris.Crdt.Managed.Model.InstanceId;
using ShardId = Nyris.Crdt.Managed.Model.ShardId;

namespace Nyris.Crdt.Transport.Abstractions;

public interface INodeClient
{
    Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas, OperationContext context);
    Task MergeMetadataAsync(MetadataDto kind, ReadOnlyMemory<byte> data, OperationContext context);
    IDuplexMetadataDeltasStream GetMetadataDuplexStream();
    IDuplexDeltasStream GetDeltaDuplexStream();
    IAsyncEnumerable<(MetadataDto, ReadOnlyMemory<byte>)> JoinToClusterAsync(NodeInfo nodeInfo,
        OperationContext context);
}