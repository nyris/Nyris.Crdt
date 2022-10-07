using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;


public static class InstanceIdExtensions
{
    public static GlobalShardId With(this InstanceId instanceId, ShardId shardId) => new(instanceId, shardId);
}
