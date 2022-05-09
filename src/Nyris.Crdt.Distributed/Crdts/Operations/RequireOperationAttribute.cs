using System;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireOperationAttribute : Attribute
    {
        public RequireOperationAttribute(Type operationType, Type responseType)
        {
            OperationType = operationType;
            ResponseType = responseType;
        }

        public Type OperationType { get; }
        public Type ResponseType { get; }
    }
}
