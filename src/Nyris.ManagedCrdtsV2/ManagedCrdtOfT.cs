using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public abstract class ManagedCrdt<TDelta, TTimeStamp> : ManagedCrdt
{
    private readonly ISerializer _serializer;

    protected ManagedCrdt(InstanceId instanceId, ISerializer serializer) : base(instanceId)
    {
        _serializer = serializer;
    }

    public abstract Task MergeDeltaBatchAsync(TDelta[] deltas, OperationContext context);
    public abstract IAsyncEnumerable<TDelta[]> EnumerateDeltaBatchesAsync(TTimeStamp since);
        
    public sealed override Task MergeDeltaBatchAsync(ReadOnlySpan<byte> deltas, OperationContext context)
        => MergeDeltaBatchAsync(_serializer.Deserialize<TDelta[]>(deltas), context);

    public sealed override async IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltaBatchesAsync(ReadOnlyMemory<byte> since)
    {
        var sinceTyped = _serializer.Deserialize<TTimeStamp>(since.Span);
        await foreach (var batch in EnumerateDeltaBatchesAsync(sinceTyped))
        {
            yield return _serializer.Serialize(batch);
        }
    }
}