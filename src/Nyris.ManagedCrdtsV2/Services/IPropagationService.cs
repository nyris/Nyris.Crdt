using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Services;

public interface IPropagationService
{
    Task PropagateAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> data, OperationContext operationContext);
}