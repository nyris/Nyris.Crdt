using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IDuplexDeltasStream : IDisposable
{
    Task<ReadOnlyMemory<byte>> ExchangeTimestampsAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> timestamp, CancellationToken cancellationToken);

    Task SendDeltasAndFinishAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> deltas, CancellationToken cancellationToken);

    IAsyncEnumerable<ReadOnlyMemory<byte>> GetDeltasAsync(CancellationToken cancellationToken);
}