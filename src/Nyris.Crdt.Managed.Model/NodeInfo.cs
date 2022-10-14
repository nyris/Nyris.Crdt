namespace Nyris.Crdt.Managed.Model;

public sealed record NodeInfo(Uri Address, NodeId Id) : IComparable<NodeInfo>
{
    /// <inheritdoc />
    public int CompareTo(NodeInfo? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        return 0;
        // return other is null ? 1 : Id.CompareTo(other.Id);
    }

    public static bool operator <(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) < 0;

    public static bool operator <=(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) <= 0;

    public static bool operator >(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) > 0;

    public static bool operator >=(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) >= 0;
}