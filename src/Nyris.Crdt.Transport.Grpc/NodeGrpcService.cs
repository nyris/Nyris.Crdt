using System.Collections.Immutable;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

    public override async Task<Empty> MergeDeltas(DeltaBatch request, ServerCallContext context)
    {
        var instanceId = InstanceId.FromChars(request.InstanceId);
        if (!_crdts.TryGet(instanceId, out var crdt))
        {
            _logger.LogWarning("Received delta batch for a non-existent crdt with instanceId '{InstanceId}', ignoring it", instanceId);
            return Empty;
        }
        var shardId = ShardId.FromUint(request.ShardId);
        await crdt.MergeAsync(shardId, request.Deltas.Memory, OperationContext.Default);
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

    public override Task<Empty> MergeMetadataDeltas(MetadataDelta request, ServerCallContext context)
    {
        _clusterMetadata.MergeAsync((MetadataDto)request.Kind, request.Deltas.Memory);
        return Task.FromResult(Empty);
    }

    public override async Task Sync(IAsyncStreamReader<DeltaBatch> requestStream, IServerStreamWriter<DeltaBatch> responseStream, ServerCallContext context)
    {
        // first exchange headers
        var timestamp = GrpcDuplexDeltasStream.TimestampFromHeaders(context.RequestHeaders);
        var (instanceId, shardId) = GrpcDuplexDeltasStream.IdsFromHeaders(context.RequestHeaders);
        if (!_crdts.TryGet(instanceId, out var crdt))
        {
            throw new KeyNotFoundException("Received instance id was not found");
        }

        var myTimestamp = crdt.GetCausalTimestamp(shardId);
        var headers = GrpcDuplexDeltasStream.ToHeaders(myTimestamp);
        await context.WriteResponseHeadersAsync(headers);

        // then exchange deltas
        await Task.WhenAll(MergeIncomingDeltas(requestStream, crdt, shardId, context.CancellationToken), 
            WriteDeltasToResponse(responseStream, crdt, shardId, timestamp, context.CancellationToken));
    }

    public override async Task SyncMetadata(IAsyncStreamReader<MetadataDelta> requestStream, IServerStreamWriter<MetadataDelta> responseStream, ServerCallContext context)
    {
        // first exchange headers
        var timestamps = GrpcDuplexMetadataDeltasStream.TimestampsFromHeaders(context.RequestHeaders);
        var myTimestamps = _clusterMetadata.GetCausalTimestamps(context.CancellationToken);
        var headers = GrpcDuplexMetadataDeltasStream.ToHeaders(myTimestamps);
        await context.WriteResponseHeadersAsync(headers);

        // then exchange deltas
        await Task.WhenAll(MergeIncomingMetadataDeltas(requestStream), 
            WriteMetadataDeltasToResponse(responseStream, timestamps, context.CancellationToken));
    }

    private async Task MergeIncomingDeltas(IAsyncStreamReader<DeltaBatch> requestStream, ManagedCrdt crdt, ShardId shardId, CancellationToken cancellationToken)
    {
        await foreach (var deltas in requestStream.ReadAllAsync(cancellationToken: cancellationToken))
        {
            _logger.LogDebug("Received delta batch for crdt ({InstanceId}, {ShardId}) of size {Size} bytes", 
                crdt.InstanceId, shardId, deltas.Deltas.Length);
            await crdt.MergeAsync(shardId, deltas.Deltas.Memory, OperationContext.Default);
        }
    }
    
    private async Task WriteDeltasToResponse(IAsyncStreamWriter<DeltaBatch> responseStream,
        ManagedCrdt crdt, 
        ShardId shardId,
        ReadOnlyMemory<byte> timestamp,
        CancellationToken cancellationToken)
    {
        await foreach (var deltas in crdt.EnumerateDeltaBatchesAsync(shardId, timestamp, cancellationToken))
        {
            _logger.LogDebug("Writing to stream a delta batch for crdt ({InstanceId}, {ShardId}) of size {Size} bytes", 
                crdt.InstanceId, shardId, deltas.Length);
            await responseStream.WriteAsync(new DeltaBatch
            {
                Deltas = UnsafeByteOperations.UnsafeWrap(deltas)
            }, cancellationToken);
        }
    }
    
    private async Task MergeIncomingMetadataDeltas(IAsyncStreamReader<MetadataDelta> requestStream)
    {
        await foreach (var deltas in requestStream.ReadAllAsync())
        {
            await _clusterMetadata.MergeAsync((MetadataDto)deltas.Kind, deltas.Deltas.Memory);
        }
    }

    private async Task WriteMetadataDeltasToResponse(IAsyncStreamWriter<MetadataDelta> responseStream,
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps,
        CancellationToken cancellationToken)
    {
        foreach (var (kind, timestamp) in timestamps)
        {
            var deltasEnumerable = _clusterMetadata.EnumerateDeltasAsync(kind, timestamp, cancellationToken);
            await foreach (var deltas in deltasEnumerable.WithCancellation(cancellationToken))
            {
                await responseStream.WriteAsync(new MetadataDelta
                {
                    Kind = (int)kind,
                    Deltas = UnsafeByteOperations.UnsafeWrap(deltas)
                }, cancellationToken);
            }
        }
    }
}