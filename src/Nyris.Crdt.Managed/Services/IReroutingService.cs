using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Services;

public interface IReroutingService
{
    Task<ReadOnlyMemory<byte>> RerouteAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> operation,
        OperationContext context);
}