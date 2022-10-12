using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public abstract class ManagedCrdt
{
    protected internal readonly SemaphoreSlim WriteLock = new(1, 1);
    
    protected ManagedCrdt(InstanceId instanceId)
    {
        InstanceId = instanceId;
    }
    
    public InstanceId InstanceId { get; }
    internal abstract ICollection<ShardId> Shards { get; }

    public abstract Task MergeAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context);

    public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ShardId shardId,
        ReadOnlyMemory<byte> causalTimestampBin,
        CancellationToken cancellationToken);

    public abstract ReadOnlyMemory<byte> GetCausalTimestamp(ShardId shardId);
}