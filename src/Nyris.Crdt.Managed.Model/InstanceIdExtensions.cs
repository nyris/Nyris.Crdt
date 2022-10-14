namespace Nyris.Crdt.Managed.Model;


public static class InstanceIdExtensions
{
    public static ReplicaId With(this InstanceId instanceId, ShardId shardId) => new(instanceId, shardId);
}
