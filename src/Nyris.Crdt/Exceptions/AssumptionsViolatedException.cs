using Nyris.Contracts.Exceptions;
using System;
using System.Runtime.Serialization;

namespace Nyris.Crdt.Exceptions;

/// <inheritdoc />
[Serializable]
public sealed class AssumptionsViolatedException : NyrisException
{
    /// <inheritdoc />
    public AssumptionsViolatedException() : base() { }

    /// <inheritdoc />
    public AssumptionsViolatedException(string message) : base(message) { }

    /// <inheritdoc />
    public AssumptionsViolatedException(string message, NyrisException inner) : base(message, inner) { }

    /// <inheritdoc />
    private AssumptionsViolatedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public AssumptionsViolatedException(string message, Exception innerException) : base(message, innerException) { }
}
