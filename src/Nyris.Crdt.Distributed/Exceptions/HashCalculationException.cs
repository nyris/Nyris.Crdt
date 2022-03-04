using System;
using System.Runtime.Serialization;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Distributed.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public sealed class HashCalculationException : NyrisException
    {
        /// <inheritdoc />
        public HashCalculationException() : base()
        {
        }

        /// <inheritdoc />
        public HashCalculationException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public HashCalculationException(string message, NyrisException inner) : base(message, inner)
        {
        }

        /// <inheritdoc />
        public HashCalculationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}