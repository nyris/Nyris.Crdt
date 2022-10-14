using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Strategies.Distribution;

public readonly struct ReplicaInfo : IComparable<ReplicaInfo>
{
    public readonly ulong Size;
    public readonly ReplicaId Id;
    public readonly uint RequestedReplicaCount;

    public ReplicaInfo(ReplicaId Id, ulong size, uint requestedReplicaCount)
    {
        Size = size;
        this.Id = Id;
        RequestedReplicaCount = requestedReplicaCount;
    }

    public int CompareTo(ReplicaInfo other) => Id.CompareTo(other.Id);
}