using System;
using System.Runtime.Serialization;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Distributed.Exceptions
{
    /// <inheritdoc />
    [Serializable]
    public sealed class GeneratedCodeExpectationsViolatedException : NyrisException
    {
        /// <inheritdoc />
        public GeneratedCodeExpectationsViolatedException() : base() { }

        /// <inheritdoc />
        public GeneratedCodeExpectationsViolatedException(string message) : base(message) { }

        /// <inheritdoc />
        public GeneratedCodeExpectationsViolatedException(string message, Exception inner) : base(message, inner) { }

        /// <inheritdoc />
        private GeneratedCodeExpectationsViolatedException(SerializationInfo info, StreamingContext context) : base(info,
                                                                                                                    context) { }
    }
}
