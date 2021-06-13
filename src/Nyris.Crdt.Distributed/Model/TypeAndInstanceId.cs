using System;

namespace Nyris.Crdt.Distributed.Model
{
    public readonly struct TypeAndInstanceId
    {
        public readonly Type Type;
        public readonly string InstanceId;

        public TypeAndInstanceId(Type type, string instanceId)
        {
            InstanceId = instanceId;
            Type = type;
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Type, InstanceId);
    }
}