using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IPropagationService
{
    Task PropagateAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}