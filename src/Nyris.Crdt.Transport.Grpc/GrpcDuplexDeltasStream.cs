using System.Runtime.CompilerServices;
using Google.Protobuf;
using Grpc.Core;
using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;
using InstanceId = Nyris.Crdt.Managed.Model.InstanceId;
using ShardId = Nyris.Crdt.Managed.Model.ShardId;

namespace Nyris.Crdt.Transport.Grpc;

internal sealed class GrpcDuplexDeltasStream : IDuplexDeltasStream
{
    private AsyncDuplexStreamingCall<CrdtBytesMsg, CrdtBytesMsg>? _call;
    private readonly Node.NodeClient _client;

    
    public GrpcDuplexDeltasStream(Node.NodeClient client)
    {
        _client = client;
    }


    public async Task<ReadOnlyMemory<byte>> ExchangeTimestampsAsync(InstanceId instanceId,
        ShardId shardId,
        ReadOnlyMemory<byte> timestamp,
        bool doNotSendDeltas,
        OperationContext context)
    {
        var headers = new Metadata()
            .WithTimestamp(timestamp)
            .WithOrigin(context.Origin)
            .With(instanceId)
            .With(shardId)
            .WithTraceId(context.TraceId);

        if (doNotSendDeltas) headers.DoNotSendDeltas();
        
        _call = _client.Sync(headers, null, context.CancellationToken);
        var responseHeaders = await _call.ResponseHeadersAsync;
        return responseHeaders.GetTimestamp();
    }

    public async Task SendDeltasAndFinishAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> deltas, CancellationToken cancellationToken)
    {
        if (_call is null)
        {
            throw new SynchronizationProtocolViolatedException($"{nameof(ExchangeTimestampsAsync)} method must be called before {nameof(SendDeltasAndFinishAsync)}");
        } 
        
        await foreach (var delta in deltas.WithCancellation(cancellationToken))
        {
            await _call.RequestStream.WriteAsync(new CrdtBytesMsg
            {
                Value = UnsafeByteOperations.UnsafeWrap(delta)
            }, cancellationToken);
        }

        await _call.RequestStream.CompleteAsync();
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> GetDeltasAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_call is null)
        {
            throw new SynchronizationProtocolViolatedException($"{nameof(ExchangeTimestampsAsync)} method must be called before {nameof(GetDeltasAsync)}");
        } 
        
        await foreach (var batch in _call.ResponseStream.ReadAllAsync(cancellationToken: cancellationToken))
        {
            yield return batch.Value.Memory;
        }
    }

    public void Dispose() => _call?.Dispose();
}