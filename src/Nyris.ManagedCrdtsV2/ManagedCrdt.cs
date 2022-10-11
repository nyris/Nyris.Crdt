using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public abstract class ManagedCrdt
{
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

    // ManagedCrdt should have everything needed by SyncAndRelocation service.
    // That is - get hash, timestamp and enumerate deltas for a given shardId 
}