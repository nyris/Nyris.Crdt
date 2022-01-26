using System;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireOperationAttribute : Attribute
    {
        public RequireOperationAttribute(Type operationType, Type responseType)
        {
        }
    }
}