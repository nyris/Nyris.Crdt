using System.Runtime.Serialization;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Managed.Exceptions;

public class BaseException : NyrisException 
{
    public BaseException()
    {
    }

    public BaseException(string? message) : base(message)
    {
    }

    public BaseException(string? message, Exception? inner) : base(message, inner)
    {
    }

    public BaseException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}