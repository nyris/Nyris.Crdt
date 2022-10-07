using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;
using Nyris.ManagedCrdtsV2;
using ShardId = Nyris.ManagedCrdtsV2.ShardId;

namespace Nyris.Crdt.Transport.Grpc;


internal sealed class NodeGrpcService : Node.NodeBase
{
    private readonly IClusterMetadataManager _clusterMetadata;
    private readonly IManagedCrdtProvider _crdts;
    private readonly ILogger<NodeGrpcService> _logger;
    private static readonly Empty Empty = new();

    public NodeGrpcService(IClusterMetadataManager clusterMetadata, IManagedCrdtProvider crdts, ILogger<NodeGrpcService> logger)
    {
        _clusterMetadata = clusterMetadata;
        _crdts = crdts;
        _logger = logger;
    }

    public override async Task<Empty> MergeDeltaBatch(DeltaBatch request, ServerCallContext context)
    {
        var instanceId = InstanceId.FromChars(request.InstanceId);
        if (!_crdts.TryGet(instanceId, out var crdt))
        {
            _logger.LogWarning("Received delta batch for a non-existent crdt with instanceId '{InstanceId}', ignoring it", instanceId);
        }
        var shardId = ShardId.FromUint(request.ShardId);
        await crdt!.MergeDeltaBatchAsync(shardId, request.Deltas.Memory, OperationContext.Default);
        return Empty;
    }

    public override async Task<NodeSetDto> JoinToCluster(NodeInfo request, ServerCallContext context)
    {
        var nodeInfo = new Distributed.Model.NodeInfo(new Uri(request.Address), NodeId.FromChars(request.NodeId));
        var dto = await _clusterMetadata.AddNewNodeAsync(nodeInfo);
        return new NodeSetDto
        {
            Value = UnsafeByteOperations.UnsafeWrap(dto)
        };
    }

    public override Task<Empty> MergeMetadataDeltaBatch(MetadataDelta request, ServerCallContext context)
    {
        _clusterMetadata.MergeAsync((MetadataDto)request.Kind, request.Deltas.Memory);
        return Task.FromResult(Empty);
    }
}