using System.Collections.Immutable;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

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

    public override async Task JoinToCluster(AddNodeMessage request, IServerStreamWriter<MetadataDelta> responseStream, ServerCallContext context)
    {
        var nodeInfo = new NodeInfo(new Uri(request.Address), NodeId.FromString(request.NodeId));
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

    public override async Task<Empty> MergeDeltas(CrdtBytesMsg request, ServerCallContext context)
    {
        var instanceId = InstanceId.FromString(request.InstanceId);
        if (!_crdts.TryGet(instanceId, out var crdt))
        {
            _logger.LogWarning("TraceId '{TraceId}': Received delta batch for a non-existent crdt with instanceId '{InstanceId}', ignoring it", 
                request.Context.TraceId, instanceId.ToString());
            return Empty;
        }
        var shardId = ShardId.FromUint(request.ShardId);
        var operationContext = GetOperationContext(request.Context, context.CancellationToken);
        await crdt.MergeAsync(shardId, request.Value.Memory, operationContext);
        return Empty;
    }
    
    public override Task<Empty> MergeMetadataDeltas(MetadataDelta request, ServerCallContext context)
    {
        var operationContext = GetOperationContext(request.Context, context.CancellationToken);
        _clusterMetadata.MergeAsync((MetadataKind)request.Kind, request.Deltas.Memory, operationContext);
        return Task.FromResult(Empty);
    }
    
    public override async Task Sync(IAsyncStreamReader<CrdtBytesMsg> requestStream, IServerStreamWriter<CrdtBytesMsg> responseStream, ServerCallContext context)
    {
        // first exchange headers
        var headers = context.RequestHeaders;
        var traceId = headers.GetTraceId();
        var instanceId = headers.GetInstanceId();
        
        if (!_crdts.TryGet(instanceId, out var crdt))
        {
            throw new KeyNotFoundException($"TraceId '{traceId}': Received instance id was not found");
        }

        var shardId = headers.GetShardId();
        var myTimestamp = crdt.GetCausalTimestamp(shardId);
        var responseHeaders = new Metadata().WithTimestamp(myTimestamp);
        await context.WriteResponseHeadersAsync(responseHeaders);

        // then exchange deltas
        var operationContextLocal = new OperationContext(headers.GetOrigin(), 0, traceId, context.CancellationToken);
        await Task.WhenAll(MergeIncomingDeltas(requestStream, crdt, instanceId, shardId, operationContextLocal), 
            WriteDeltasToResponse(responseStream, crdt, instanceId, shardId, headers.GetTimestamp(), traceId, context.CancellationToken));
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

    public override async Task<CrdtBytesMsg> Reroute(CrdtBytesMsg request, ServerCallContext context)
    {
        var instanceId = InstanceId.FromString(request.InstanceId);
        if (!_crdts.TryGet(instanceId, out var crdt))
        {
            throw new RoutingException($"TraceId '{request.Context.TraceId}': Received operation for " +
                                       $"a non-existent crdt with instanceId '{instanceId.ToString()}'");
        }
        var shardId = ShardId.FromUint(request.ShardId);
        var operationContext = GetOperationContext(request.Context, context.CancellationToken);
        var result = await crdt.ApplyAsync(shardId, request.Value.Memory, operationContext);
        return new CrdtBytesMsg
        {
            Value = UnsafeByteOperations.UnsafeWrap(result)
        };
    }

    private static OperationContext GetOperationContext(OperationContextMessage? contextDto, CancellationToken cancellationToken)
    {
        if (contextDto is null) return new OperationContext(NodeId.Empty, 0, "", cancellationToken);

        var origin = string.IsNullOrEmpty(contextDto.Origin) ? NodeId.Empty : NodeId.FromString(contextDto.Origin);
        var operationContext = new OperationContext(origin, contextDto.AwaitPropagationToNNodes,
            contextDto.TraceId, cancellationToken);
        return operationContext;
    }

    private async Task MergeIncomingDeltas(IAsyncStreamReader<CrdtBytesMsg> requestStream, IManagedCrdt crdt, InstanceId instanceId, ShardId shardId, OperationContext context)
    {
        await foreach (var deltas in requestStream.ReadAllAsync(cancellationToken: context.CancellationToken))
        {
            _logger.LogDebug("TraceId '{TraceId}': Received delta batch for crdt ({InstanceId}, {ShardId}) of size {Size} bytes", 
                context.TraceId, instanceId.ToString(), shardId.ToString(), deltas.Value.Length.ToString());
            await crdt.MergeAsync(shardId, deltas.Value.Memory, context);
        }
    }
    
    private async Task WriteDeltasToResponse(IAsyncStreamWriter<CrdtBytesMsg> responseStream,
        IManagedCrdt crdt,
        InstanceId instanceId,
        ShardId shardId,
        ReadOnlyMemory<byte> timestamp,
        string traceId,
        CancellationToken cancellationToken)
    {
        await foreach (var deltas in crdt.EnumerateDeltaBatchesAsync(shardId, timestamp, cancellationToken))
        {
            _logger.LogDebug("TraceId '{TraceId}': Writing to stream a delta batch for crdt ({InstanceId}, {ShardId}) of size {Size} bytes", 
                traceId, instanceId.ToString(), shardId.ToString(), deltas.Length.ToString());
            await responseStream.WriteAsync(new CrdtBytesMsg
            {
                Value = UnsafeByteOperations.UnsafeWrap(deltas)
            }, cancellationToken);
        }
    }
    
    private async Task MergeIncomingMetadataDeltas(IAsyncStreamReader<MetadataDelta> requestStream, OperationContext context)
    {
        await foreach (var deltas in requestStream.ReadAllAsync())
        {
            await _clusterMetadata.MergeAsync((MetadataKind)deltas.Kind, deltas.Deltas.Memory, context);
        }
    }

    private async Task WriteMetadataDeltasToResponse(IAsyncStreamWriter<MetadataDelta> responseStream,
        ImmutableDictionary<MetadataKind, ReadOnlyMemory<byte>> timestamps,
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