using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public abstract class ManagedCrdt
{
    protected ManagedCrdt(InstanceId instanceId)
    {
        InstanceId = instanceId;
    }
    
    public InstanceId InstanceId { get; }

    public abstract Task MergeDeltaBatchAsync(ReadOnlySpan<byte> deltas, OperationContext context);

    public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ReadOnlyMemory<byte> since);

    public abstract ReadOnlyMemory<byte> GetHash();
}