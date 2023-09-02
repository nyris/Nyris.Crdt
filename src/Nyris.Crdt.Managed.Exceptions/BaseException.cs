using System.Runtime.Serialization;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Managed.Exceptions;

[Serializable]
public abstract class BaseException : NyrisException
{
    protected BaseException()
    {
    }

    protected BaseException(string? message) : base(message)
    {
    }

    protected BaseException(string? message, Exception? inner) : base(message, inner)
    {
    }

    protected BaseException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}