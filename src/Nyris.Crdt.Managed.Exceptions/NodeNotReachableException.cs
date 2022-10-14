using System.Runtime.Serialization;

namespace Nyris.Crdt.Managed.Exceptions;

public sealed class NodeNotReachableException : BaseException
{
    public NodeNotReachableException()
    {
    }

    public NodeNotReachableException(string? message) : base(message)
    {
    }

    public NodeNotReachableException(string? message, Exception? inner) : base(message, inner)
    {
    }

    public NodeNotReachableException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}