using System.Runtime.Serialization;

namespace Nyris.Crdt.Managed.Exceptions;

/// <inheritdoc />
[Serializable]
public class InitializationException : BaseException
{
    /// <inheritdoc />
    public InitializationException() : base() { }

    /// <inheritdoc />
    public InitializationException(string message) : base(message) { }

    /// <inheritdoc />
    public InitializationException(string message, Exception inner) : base(message, inner) { }

    /// <inheritdoc />
    protected InitializationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
