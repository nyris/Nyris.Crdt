using System.Runtime.Serialization;

namespace Nyris.Crdt.Managed.Exceptions;

public sealed class SynchronizationProtocolViolatedException : BaseException 
{
    public SynchronizationProtocolViolatedException()
    {
    }

    public SynchronizationProtocolViolatedException(string? message) : base(message)
    {
    }

    public SynchronizationProtocolViolatedException(string? message, Exception? inner) : base(message, inner)
    {
    }

    public SynchronizationProtocolViolatedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}