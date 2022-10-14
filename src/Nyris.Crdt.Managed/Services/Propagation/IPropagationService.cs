using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Services.Propagation;

public interface IPropagationService
{
    Task PropagateAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> data, OperationContext operationContext);
}