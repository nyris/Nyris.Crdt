using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Services;

public interface IReroutingService
{
    Task<ReadOnlyMemory<byte>> RerouteAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> operation,
        OperationContext context);
}