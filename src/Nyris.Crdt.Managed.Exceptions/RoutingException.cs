using System.Runtime.Serialization;

namespace Nyris.Crdt.Managed.Exceptions;

[Serializable]
public sealed class RoutingException : BaseException
{
    public RoutingException()
    {
    }

    public RoutingException(string? message) : base(message)
    {
    }

    public RoutingException(string? message, Exception? inner) : base(message, inner)
    {
    }

    public RoutingException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}