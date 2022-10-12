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
            _logger.LogWarning("TraceId '{TraceId}': Received delta batch for a non-existent crdt with instanceId '{InstanceId}', ignoring it", 
                request.Context.TraceId, instanceId);
            return Empty;
        }
        var shardId = ShardId.FromUint(request.ShardId);
        var operationContext = GetOperationContext(request.Context, context.CancellationToken);
        await crdt.MergeAsync(shardId, request.Deltas.Memory, operationContext);
        return Empty;
    }

    public override async Task JoinToCluster(AddNodeMessage request, IServerStreamWriter<MetadataDelta> responseStream, ServerCallContext context)
    {
        var nodeInfo = new NodeInfo(new Uri(request.Address), NodeId.FromChars(request.NodeId));
        var operationContext = GetOperationContext(request.Context, context.CancellationToken);
        await foreach (var (kind, bin) in _clusterMetadata.AddNewNodeAsync(nodeInfo, operationContext))
        {
            await responseStream.WriteAsync(new MetadataDelta
            {
                Kind = (int)kind,
                Deltas = UnsafeByteOperations.UnsafeWrap(bin)
            });
        }
    }
    
    public override Task<Empty> MergeMetadataDeltas(MetadataDelta request, ServerCallContext context)
    {
        var operationContext = GetOperationContext(request.Context, context.CancellationToken);
        _clusterMetadata.MergeAsync((MetadataDto)request.Kind, request.Deltas.Memory, operationContext);
        return Task.FromResult(Empty);
    }

    public override async Task Sync(IAsyncStreamReader<DeltaBatch> requestStream, IServerStreamWriter<DeltaBatch> responseStream, ServerCallContext context)
    {
        // first exchange headers
        var headers = context.RequestHeaders;
        var traceId = headers.GetTraceId();
        
        if (!_crdts.TryGet(headers.GetInstanceId(), out var crdt))
        {
            throw new KeyNotFoundException($"TraceId '{traceId}': Received instance id was not found");
        }

        var shardId = headers.GetShardId();
        var myTimestamp = crdt.GetCausalTimestamp(shardId);
        var responseHeaders = new Metadata().WithTimestamp(myTimestamp);
        await context.WriteResponseHeadersAsync(responseHeaders);

        // then exchange deltas
        var operationContextLocal = new OperationContext(headers.GetOrigin(), 0, traceId, context.CancellationToken);
        await Task.WhenAll(MergeIncomingDeltas(requestStream, crdt, shardId, operationContextLocal), 
            WriteDeltasToResponse(responseStream, crdt, shardId, headers.GetTimestamp(), traceId, context.CancellationToken));
    }

    public override async Task SyncMetadata(IAsyncStreamReader<MetadataDelta> requestStream, IServerStreamWriter<MetadataDelta> responseStream, ServerCallContext context)
    {
        var headers = context.RequestHeaders;
        var operationContext = new OperationContext(headers.GetOrigin(), 
            0,
            headers.GetTraceId(),
            context.CancellationToken);
        
        // first exchange headers
        var timestamps = GrpcDuplexMetadataDeltasStream.TimestampsFromHeaders(context.RequestHeaders);
        var myTimestamps = _clusterMetadata.GetCausalTimestamps(context.CancellationToken);
        var responseHeaders = GrpcDuplexMetadataDeltasStream.ToHeaders(myTimestamps);
        await context.WriteResponseHeadersAsync(responseHeaders);

        // then exchange deltas
        await Task.WhenAll(MergeIncomingMetadataDeltas(requestStream, operationContext), 
            WriteMetadataDeltasToResponse(responseStream, timestamps, context.CancellationToken));
    }

    private OperationContext GetOperationContext(OperationContextMessage? contextDto, CancellationToken cancellationToken)
    {
        if (contextDto is null)
        {
            return new OperationContext(NodeId.Empty, 0, "", cancellationToken);
        }
        
        var origin = string.IsNullOrEmpty(contextDto.Origin) ? NodeId.Empty : NodeId.FromChars(contextDto.Origin);
        var operationContext = new OperationContext(origin, contextDto.AwaitPropagationToNNodes,
            contextDto.TraceId, cancellationToken);
        return operationContext;
    }

    private async Task MergeIncomingDeltas(IAsyncStreamReader<DeltaBatch> requestStream, ManagedCrdt crdt, ShardId shardId, OperationContext context)
    {
        await foreach (var deltas in requestStream.ReadAllAsync(cancellationToken: context.CancellationToken))
        {
            _logger.LogDebug("TraceId '{TraceId}': Received delta batch for crdt ({InstanceId}, {ShardId}) of size {Size} bytes", 
                context.TraceId, crdt.InstanceId, shardId, deltas.Deltas.Length);
            await crdt.MergeAsync(shardId, deltas.Deltas.Memory, context);
        }
    }
    
    private async Task WriteDeltasToResponse(IAsyncStreamWriter<DeltaBatch> responseStream,
        ManagedCrdt crdt, 
        ShardId shardId,
        ReadOnlyMemory<byte> timestamp,
        string traceId,
        CancellationToken cancellationToken)
    {
        await foreach (var deltas in crdt.EnumerateDeltaBatchesAsync(shardId, timestamp, cancellationToken))
        {
            _logger.LogDebug("TraceId '{TraceId}': Writing to stream a delta batch for crdt ({InstanceId}, {ShardId}) of size {Size} bytes", 
                traceId, crdt.InstanceId, shardId, deltas.Length);
            await responseStream.WriteAsync(new DeltaBatch
            {
                Deltas = UnsafeByteOperations.UnsafeWrap(deltas)
            }, cancellationToken);
        }
    }
    
    private async Task MergeIncomingMetadataDeltas(IAsyncStreamReader<MetadataDelta> requestStream, OperationContext context)
    {
        await foreach (var deltas in requestStream.ReadAllAsync())
        {
            await _clusterMetadata.MergeAsync((MetadataDto)deltas.Kind, deltas.Deltas.Memory, context);
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