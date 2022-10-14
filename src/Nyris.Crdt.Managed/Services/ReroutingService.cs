using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Services;

internal sealed class ReroutingService : IReroutingService
{
    
    public Task<ReadOnlyMemory<byte>> RerouteAsync(InstanceId instanceId, ShardId shardId, ReadOnlyMemory<byte> operation, OperationContext context)
    {
        throw new NotImplementedException();
    }
}