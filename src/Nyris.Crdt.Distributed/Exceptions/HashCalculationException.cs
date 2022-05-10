using Nyris.Contracts.Exceptions;
using System;
using System.Runtime.Serialization;

namespace Nyris.Crdt.Distributed.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public sealed class HashCalculationException : NyrisException
    {
        /// <inheritdoc />
        public HashCalculationException() : base() { }

        /// <inheritdoc />
        public HashCalculationException(string message) : base(message) { }

        /// <inheritdoc />
        public HashCalculationException(string message, NyrisException inner) : base(message, inner) { }

        /// <inheritdoc />
        private HashCalculationException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public HashCalculationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
