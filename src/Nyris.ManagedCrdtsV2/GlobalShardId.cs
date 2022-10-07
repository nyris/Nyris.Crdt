using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public readonly struct GlobalShardId : IComparable<GlobalShardId>, IEquatable<GlobalShardId>
{
    public readonly InstanceId InstanceId;
    public readonly ShardId ShardId;

    public GlobalShardId(InstanceId instanceId, ShardId shardId)
    {
        InstanceId = instanceId;
        ShardId = shardId;
    }

    public bool Equals(GlobalShardId other) => InstanceId.Equals(other.InstanceId) && ShardId.Equals(other.ShardId);
    public override bool Equals(object? obj) => obj is GlobalShardId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InstanceId, ShardId);

    public static bool operator ==(GlobalShardId lhs, GlobalShardId rhs) => lhs.Equals(rhs);
    public static bool operator !=(GlobalShardId lhs, GlobalShardId rhs) => !lhs.Equals(rhs);
    public static bool operator <(GlobalShardId lhs, GlobalShardId rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >(GlobalShardId lhs, GlobalShardId rhs) => lhs.CompareTo(rhs) > 0;
    public static bool operator <=(GlobalShardId lhs, GlobalShardId rhs) => lhs.CompareTo(rhs) <= 0;
    public static bool operator >=(GlobalShardId lhs, GlobalShardId rhs) => lhs.CompareTo(rhs) >= 0;
    
    public int CompareTo(GlobalShardId other)
    {
        var instanceIdComparison = InstanceId.CompareTo(other.InstanceId);
        return instanceIdComparison != 0 ? instanceIdComparison : ShardId.CompareTo(other.ShardId);
    }
}