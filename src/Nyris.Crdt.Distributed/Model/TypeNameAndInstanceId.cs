using System;

namespace Nyris.Crdt.Distributed.Model
{
    public readonly struct TypeNameAndInstanceId
    {
        public readonly string TypeName;
        public readonly int InstanceId;

        public TypeNameAndInstanceId(string typeName, int instanceId)
        {
            InstanceId = instanceId;
            TypeName = typeName;
        }

        public void Deconstruct(out string typeName, out int instanceId)
        {
            typeName = TypeName;
            instanceId = InstanceId;
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(TypeName, InstanceId);
    }
}