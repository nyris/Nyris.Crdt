using Nyris.Contracts.Exceptions;
using System;
using System.Runtime.Serialization;

namespace Nyris.Crdt.Distributed.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public sealed class ManagedCrdtContextException : NyrisException
    {
        public string? PropertyName { get; }

        /// <inheritdoc />
        public ManagedCrdtContextException(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <inheritdoc />
        public ManagedCrdtContextException(string message, string propertyName) : base(message)
        {
            PropertyName = propertyName;
        }

        /// <inheritdoc />
        public ManagedCrdtContextException(string message, Exception inner, string propertyName) : base(message, inner)
        {
            PropertyName = propertyName;
        }

        /// <inheritdoc />
        private ManagedCrdtContextException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public ManagedCrdtContextException() { }

        public ManagedCrdtContextException(string message, Exception innerException) : base(message, innerException) { }
    }
}
