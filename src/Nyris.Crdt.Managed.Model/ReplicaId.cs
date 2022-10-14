namespace Nyris.Crdt.Managed.Model;

public readonly struct ReplicaId : IComparable<ReplicaId>, IEquatable<ReplicaId>
{
    public readonly InstanceId InstanceId;
    public readonly ShardId ShardId;

    public ReplicaId(InstanceId instanceId, ShardId shardId)
    {
        InstanceId = instanceId;
        ShardId = shardId;
    }

    public bool Equals(ReplicaId other) => InstanceId.Equals(other.InstanceId) && ShardId.Equals(other.ShardId);
    public override bool Equals(object? obj) => obj is ReplicaId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InstanceId, ShardId);

    public override string ToString() => $"({InstanceId.ToString()}, {ShardId.AsUint})";

    public static bool operator ==(ReplicaId lhs, ReplicaId rhs) => lhs.Equals(rhs);
    public static bool operator !=(ReplicaId lhs, ReplicaId rhs) => !lhs.Equals(rhs);
    public static bool operator <(ReplicaId lhs, ReplicaId rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >(ReplicaId lhs, ReplicaId rhs) => lhs.CompareTo(rhs) > 0;
    public static bool operator <=(ReplicaId lhs, ReplicaId rhs) => lhs.CompareTo(rhs) <= 0;
    public static bool operator >=(ReplicaId lhs, ReplicaId rhs) => lhs.CompareTo(rhs) >= 0;
    
    public int CompareTo(ReplicaId other)
    {
        // var instanceIdComparison = InstanceId.CompareTo(other.InstanceId);
        // return instanceIdComparison != 0 ? instanceIdComparison : ShardId.CompareTo(other.ShardId);
        return 1;
    }
}