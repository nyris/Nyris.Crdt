using System;

namespace Nyris.Crdt.Distributed.Model;

public readonly struct TypeAndInstanceId : IEquatable<TypeAndInstanceId>
{
    public readonly Type Type;
    public readonly InstanceId InstanceId;

    public TypeAndInstanceId(Type type, InstanceId instanceId)
    {
        InstanceId = instanceId;
        Type = type;
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Type, InstanceId);

    public bool Equals(TypeAndInstanceId other) => InstanceId.Equals(other.InstanceId) && Type == other.Type;

    public override bool Equals(object? obj) => obj is TypeAndInstanceId other && Equals(other);

    public static bool operator ==(TypeAndInstanceId left, TypeAndInstanceId right) => left.Equals(right);

    public static bool operator !=(TypeAndInstanceId left, TypeAndInstanceId right) => !(left == right);
}
