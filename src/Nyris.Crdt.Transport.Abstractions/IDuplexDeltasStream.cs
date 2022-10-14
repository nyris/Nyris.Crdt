using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IDuplexDeltasStream : IDisposable
{
    Task<ReadOnlyMemory<byte>> ExchangeTimestampsAsync(InstanceId instanceId,
        ShardId shardId,
        ReadOnlyMemory<byte> timestamp,
        OperationContext context);

    Task SendDeltasAndFinishAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> deltas, CancellationToken cancellationToken);

    IAsyncEnumerable<ReadOnlyMemory<byte>> GetDeltasAsync(CancellationToken cancellationToken);
}