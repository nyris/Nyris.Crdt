using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.ManagedCrdts;

public abstract class ManagedCrdt : IManagedCrdt
{
    protected internal readonly SemaphoreSlim WriteLock = new(1, 1);
    
    protected ManagedCrdt(InstanceId instanceId)
    {
        InstanceId = instanceId;
    }
    
    public InstanceId InstanceId { get; }
    internal abstract ICollection<ShardId> ShardIds { get; }
    internal abstract Dictionary<ShardId, int> GetShardSizes();
    internal abstract void MarkLocalShardAsReadReplica(in ShardId shardId);

    protected internal abstract Task DropShardAsync(in ShardId shardId);

    public abstract Task MergeAsync(ShardId shardId, ReadOnlyMemory<byte> batch, OperationContext context);
    public virtual Task<ReadOnlyMemory<byte>> ApplyAsync(ShardId shardId, ReadOnlyMemory<byte> operation,
        OperationContext context)
    {
        // not an abstract cause not all descendents require rerouting, abstract would force them to implement
        throw new NotImplementedException();
    }

    public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ShardId shardId,
        ReadOnlyMemory<byte> causalTimestampBin,
        CancellationToken cancellationToken);

    public abstract ReadOnlyMemory<byte> GetCausalTimestamp(in ShardId shardId);
}