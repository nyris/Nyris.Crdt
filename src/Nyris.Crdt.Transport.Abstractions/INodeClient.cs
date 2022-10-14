using Nyris.Crdt.Managed.Model;
using InstanceId = Nyris.Crdt.Managed.Model.InstanceId;
using ShardId = Nyris.Crdt.Managed.Model.ShardId;

namespace Nyris.Crdt.Transport.Abstractions;

public interface INodeClient
{
    Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas, OperationContext context);
    Task MergeMetadataAsync(MetadataKind kind, ReadOnlyMemory<byte> data, OperationContext context);
    IDuplexMetadataDeltasStream GetMetadataDuplexStream();
    IDuplexDeltasStream GetDeltaDuplexStream();
    IAsyncEnumerable<(MetadataKind, ReadOnlyMemory<byte>)> JoinToClusterAsync(NodeInfo nodeInfo,
        OperationContext context);
}