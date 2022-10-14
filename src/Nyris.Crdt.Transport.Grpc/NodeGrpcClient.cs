using Google.Protobuf;
using Grpc.Core;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;
using InstanceId = Nyris.Crdt.Managed.Model.InstanceId;
using ShardId = Nyris.Crdt.Managed.Model.ShardId;

namespace Nyris.Crdt.Transport.Grpc;

internal sealed class NodeGrpcClient : INodeClient
{
    private readonly Node.NodeClient _client;

    public NodeGrpcClient(Node.NodeClient client)
    {
        _client = client;
    }
    
    public async Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas,
        OperationContext context)
    {
        await _client.MergeDeltasAsync(new DeltaBatch
        {
            InstanceId = instanceId.ToString(),
            ShardId = shardId.AsUint,
            Deltas = UnsafeByteOperations.UnsafeWrap(deltas),
            Context = GetContextMessage(context)
        }, cancellationToken: context.CancellationToken);
    }

    public async Task MergeMetadataAsync(MetadataKind kind, ReadOnlyMemory<byte> data,
        OperationContext context)
    {
        await _client.MergeMetadataDeltasAsync(new MetadataDelta
        {
            Kind = (int)kind,
            Deltas = UnsafeByteOperations.UnsafeWrap(data),
            Context = GetContextMessage(context)
        }, cancellationToken: context.CancellationToken);
    }

    public IDuplexMetadataDeltasStream GetMetadataDuplexStream() => new GrpcDuplexMetadataDeltasStream(_client);
    public IDuplexDeltasStream GetDeltaDuplexStream() => new GrpcDuplexDeltasStream(_client);

    public async IAsyncEnumerable<(MetadataKind, ReadOnlyMemory<byte>)> JoinToClusterAsync(
        NodeInfo nodeInfo, 
        OperationContext context)
    {
        var response = _client.JoinToCluster(new AddNodeMessage
        {
            NodeId = nodeInfo.Id.ToString(),
            Address = nodeInfo.Address.ToString(),
            Context = GetContextMessage(context)
        }, cancellationToken: context.CancellationToken);

        await foreach (var delta in response.ResponseStream.ReadAllAsync())
        {
            yield return ((MetadataKind)delta.Kind, delta.Deltas.Memory);
        }
    }

    private static OperationContextMessage GetContextMessage(OperationContext context) =>
        new()
        {
            AwaitPropagationToNNodes = context.AwaitPropagationToNNodes,
            Origin = context.Origin.ToString(),
            TraceId = context.TraceId
        };
}