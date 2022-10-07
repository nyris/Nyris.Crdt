using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public abstract class ManagedCrdt
{
    protected ManagedCrdt(InstanceId instanceId)
    {
        InstanceId = instanceId;
    }
    
    public InstanceId InstanceId { get; }

    public abstract Task MergeDeltaBatchAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context);

    public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ShardId shardId, ReadOnlyMemory<byte> since);

    public abstract ReadOnlyMemory<byte> GetHash(ShardId shardId);

    // ManagedCrdt should have everything needed by SyncAndRelocation service.
    // That is - get hash, timestamp and enumerate deltas for a given shardId 
}