using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Grpc.Core;
using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Transport.Grpc;

internal sealed class GrpcDuplexMetadataDeltasStream : IDuplexMetadataDeltasStream
{
    private AsyncDuplexStreamingCall<MetadataDelta, MetadataDelta>? _call;
    private readonly Node.NodeClient _client;

    public GrpcDuplexMetadataDeltasStream(Node.NodeClient client)
    {
        _client = client;
    }

    public static ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> TimestampsFromHeaders(Metadata responseHeaders)
    {
        var builder = ImmutableDictionary.CreateBuilder<MetadataDto, ReadOnlyMemory<byte>>();
        foreach (var header in responseHeaders)
        {
            if (Enum.TryParse<MetadataDto>(header.Key, out var kind))
            {
                builder.Add(kind, Convert.FromBase64String(header.Value));
            }
        }

        return builder.ToImmutable();
    }

    public static Metadata ToHeaders(ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps)
    {
        var headers = new Metadata();
        foreach (var (kind, timestamp) in timestamps)
        {
            // header accepts binary values as well, but only as arrays (making at least 1 copy), which it then converts to base64 anyway 
            headers.Add(((int)kind).ToString(), Convert.ToBase64String(timestamp.Span));
        }

        return headers;
    }

    public async Task<ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>>> ExchangeMetadataTimestampsAsync(
        ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> timestamps,
        OperationContext context)
    {
        var headers = ToHeaders(timestamps).WithOrigin(context.Origin).WithTraceId(context.TraceId);
        _call = _client.SyncMetadata(headers, null, context.CancellationToken);
        var responseHeaders = await _call.ResponseHeadersAsync;
        return TimestampsFromHeaders(responseHeaders);
    }

    public async Task SendDeltasAsync(MetadataDto kind, IAsyncEnumerable<ReadOnlyMemory<byte>> deltas, CancellationToken cancellationToken)
    {
        if (_call is null)
        {
            throw new SynchronizationProtocolViolatedException($"{nameof(ExchangeMetadataTimestampsAsync)} method must be called before {nameof(SendDeltasAsync)}");
        } 
            
        await foreach (var delta in deltas.WithCancellation(cancellationToken))
        {
            await _call.RequestStream.WriteAsync(new MetadataDelta
            {
                Kind = (int)kind,
                Deltas = UnsafeByteOperations.UnsafeWrap(delta)
            }, cancellationToken);
        }
    }

    public async IAsyncEnumerable<(MetadataDto kind, ReadOnlyMemory<byte>)> GetDeltasAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_call is null)
        {
            throw new SynchronizationProtocolViolatedException($"{nameof(ExchangeMetadataTimestampsAsync)} method must be called before {nameof(GetDeltasAsync)}");
        } 
        
        await foreach (var batch in _call.ResponseStream.ReadAllAsync(cancellationToken: cancellationToken))
        {
            yield return ((MetadataDto)batch.Kind, batch.Deltas.Memory);
        }
    }

    public Task FinishSendingAsync() => _call?.RequestStream.CompleteAsync() ?? Task.CompletedTask;
    public void Dispose() => _call?.Dispose();
}