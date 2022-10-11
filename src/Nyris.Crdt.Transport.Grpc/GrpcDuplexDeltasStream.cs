using System.Diagnostics;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Grpc.Core;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Exceptions;
using Nyris.ManagedCrdtsV2;
using ShardId = Nyris.ManagedCrdtsV2.ShardId;

namespace Nyris.Crdt.Transport.Grpc;

internal sealed class GrpcDuplexDeltasStream : IDuplexDeltasStream
{
    private AsyncDuplexStreamingCall<DeltaBatch, DeltaBatch>? _call;
    private readonly Node.NodeClient _client;

    private const string InstanceIdHeaderKey = "i";
    private const string ShardIdHeaderKey = "s";
    private const string TimestampHeaderKey = "t";
    
    public GrpcDuplexDeltasStream(Node.NodeClient client)
    {
        _client = client;
    }

    public static Metadata ToHeaders(in ReadOnlyMemory<byte> timestamp) =>
        new()
        {
            {TimestampHeaderKey, Convert.ToBase64String(timestamp.Span)}
        };
    
    private static Metadata ToHeaders(in InstanceId instanceId, in ShardId shardId, in ReadOnlyMemory<byte> timestamp) =>
        new()
        {
            {InstanceIdHeaderKey, instanceId.ToString()},
            {ShardIdHeaderKey, shardId.ToString()},
            {TimestampHeaderKey, Convert.ToBase64String(timestamp.Span)}
        };

    public static ReadOnlyMemory<byte> TimestampFromHeaders(Metadata headers)
    {
        var value = headers.GetValue(TimestampHeaderKey);
        return value is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(value);
    }

    public static (InstanceId, ShardId) IdsFromHeaders(Metadata headers) 
        => (InstanceId.FromChars(headers.GetValue(InstanceIdHeaderKey)), 
            ShardId.FromString(headers.GetValue(ShardIdHeaderKey)!));

    public async Task<ReadOnlyMemory<byte>> ExchangeTimestampsAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> timestamp,
        CancellationToken cancellationToken)
    {
        var headers = ToHeaders(instanceId, shardId, timestamp);
        _call = _client.Sync(headers, null, cancellationToken);
        var responseHeaders = await _call.ResponseHeadersAsync;
        return TimestampFromHeaders(responseHeaders);
    }

    public async Task SendDeltasAndFinishAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> deltas, CancellationToken cancellationToken)
    {
        if (_call is null)
        {
            throw new AssumptionsViolatedException($"{nameof(ExchangeTimestampsAsync)} method must be called before {nameof(SendDeltasAndFinishAsync)}");
        } 
        
        await foreach (var delta in deltas.WithCancellation(cancellationToken))
        {
            await _call.RequestStream.WriteAsync(new DeltaBatch
            {
                Deltas = UnsafeByteOperations.UnsafeWrap(delta)
            }, cancellationToken);
        }

        await _call.RequestStream.CompleteAsync();
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> GetDeltasAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_call is null)
        {
            throw new AssumptionsViolatedException($"{nameof(ExchangeTimestampsAsync)} method must be called before {nameof(GetDeltasAsync)}");
        } 
        
        await foreach (var batch in _call.ResponseStream.ReadAllAsync(cancellationToken: cancellationToken))
        {
            yield return batch.Deltas.Memory;
        }
    }

    public void Dispose() => _call?.Dispose();
}