using Google.Protobuf;
using Nyris.Crdt.Distributed.Model;
using Nyris.ManagedCrdtsV2;
using ShardId = Nyris.ManagedCrdtsV2.ShardId;

namespace Nyris.Crdt.Transport.Grpc;

internal sealed class NodeGrpcClient : INodeClient
{
    private readonly Node.NodeClient _client;

    public NodeGrpcClient(Node.NodeClient client)
    {
        _client = client;
    }
    
    public async Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas,
        CancellationToken cancellationToken = default)
    {
        await _client.MergeDeltasAsync(new DeltaBatch
        {
            InstanceId = instanceId.ToString(),
            ShardId = shardId.AsUint,
            Deltas = UnsafeByteOperations.UnsafeWrap(deltas)
        }, cancellationToken: cancellationToken);
    }

    public async Task MergeMetadataAsync(MetadataDto kind, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        await _client.MergeMetadataDeltasAsync(new MetadataDelta
        {
            Kind = (int)kind,
            Deltas = UnsafeByteOperations.UnsafeWrap(data)
        }, cancellationToken: cancellationToken);
    }

    public IDuplexMetadataDeltasStream GetMetadataDuplexStream() => new GrpcDuplexMetadataDeltasStream(_client);
    public IDuplexDeltasStream GetDeltaDuplexStream() => new GrpcDuplexDeltasStream(_client);

    public async Task<ReadOnlyMemory<byte>> JoinToClusterAsync(
        Distributed.Model.NodeInfo nodeInfo, 
        CancellationToken cancellationToken = default)
    {
        var response = await _client.JoinToClusterAsync(new NodeInfo
        {
            NodeId = nodeInfo.Id.ToString(),
            Address = nodeInfo.Address.ToString()
        }, cancellationToken: cancellationToken);

        return response.Value.Memory;
    }
}