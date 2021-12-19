using System;

namespace Nyris.Crdt
{
    public interface IHashable
    {
        ReadOnlySpan<byte> CalculateHash();
    }
}