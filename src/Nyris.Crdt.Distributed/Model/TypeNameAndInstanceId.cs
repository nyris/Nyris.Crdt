using System;

namespace Nyris.Crdt.Distributed.Model
{
    public readonly struct TypeNameAndInstanceId
    {
        public readonly string TypeName;
        public readonly string InstanceId;

        public TypeNameAndInstanceId(string typeName, string instanceId)
        {
            InstanceId = instanceId;
            TypeName = typeName;
        }

        public void Deconstruct(out string typeName, out string instanceId)
        {
            typeName = TypeName;
            instanceId = InstanceId;
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(TypeName, InstanceId);
    }
}