using System;
using System.Runtime.Serialization;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Distributed.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public  class InitializationException : NyrisException
    {
        /// <inheritdoc />
        public InitializationException() : base()
        {
        }

        /// <inheritdoc />
        public InitializationException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public InitializationException(string message, Exception inner) : base(message, inner)
        {
        }

        /// <inheritdoc />
        public InitializationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}