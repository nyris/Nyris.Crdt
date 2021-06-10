using System;
using System.Runtime.Serialization;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public sealed class AssumptionsViolatedException : NyrisException
    {
        /// <inheritdoc />
        public AssumptionsViolatedException() : base()
        {
        }

        /// <inheritdoc />
        public AssumptionsViolatedException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public AssumptionsViolatedException(string message, NyrisException inner) : base(message, inner)
        {
        }

        /// <inheritdoc />
        public AssumptionsViolatedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}