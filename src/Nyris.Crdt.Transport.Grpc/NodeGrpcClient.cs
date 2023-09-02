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

    public async Task MergeAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> deltas,
        OperationContext context)
    {
        await _client.MergeDeltasAsync(new CrdtBytesMsg
        {
            InstanceId = instanceId.ToString(),
            ShardId = shardId.AsUint,
            Value = UnsafeByteOperations.UnsafeWrap(deltas),
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

    public IDuplexDeltasStream GetDeltaDuplexStream() => new GrpcDuplexDeltasStream(_client);
    public IDuplexMetadataDeltasStream GetMetadataDuplexStream() => new GrpcDuplexMetadataDeltasStream(_client);

    public async Task<ReadOnlyMemory<byte>> RerouteAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> operation, OperationContext context)
    {
        var result = await _client.RerouteAsync(new CrdtBytesMsg
        {
            InstanceId = instanceId.ToString(),
            ShardId = shardId.AsUint,
            Value = UnsafeByteOperations.UnsafeWrap(operation),
            Context = GetContextMessage(context)
        }, cancellationToken: context.CancellationToken);
        return result.Value.Memory;
    }

    private static OperationContextMessage GetContextMessage(OperationContext context) =>
        new()
        {
            AwaitPropagationToNNodes = context.AwaitPropagationToNNodes,
            Origin = context.Origin.ToString(),
            TraceId = context.TraceId
        };
}