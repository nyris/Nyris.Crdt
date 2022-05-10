using Nyris.Contracts.Exceptions;
using System;
using System.Runtime.Serialization;

namespace Nyris.Crdt.Distributed.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public sealed class ManagedCrdtContextSetupException : NyrisException
    {
        /// <inheritdoc />
        public ManagedCrdtContextSetupException() : base() { }

        /// <inheritdoc />
        public ManagedCrdtContextSetupException(string message) : base(message) { }

        /// <inheritdoc />
        public ManagedCrdtContextSetupException(string message, Exception inner) : base(message, inner) { }

        /// <inheritdoc />
        private ManagedCrdtContextSetupException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
