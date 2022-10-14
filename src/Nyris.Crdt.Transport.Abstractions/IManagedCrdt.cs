using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IManagedCrdt
{
    Task MergeAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context);
    Task<ReadOnlyMemory<byte>> ApplyAsync(ShardId shardId, ReadOnlyMemory<byte> operation,
        OperationContext context);
    
    IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ShardId shardId,
        ReadOnlyMemory<byte> causalTimestampBin,
        CancellationToken cancellationToken);

    ReadOnlyMemory<byte> GetCausalTimestamp(ShardId shardId);
}