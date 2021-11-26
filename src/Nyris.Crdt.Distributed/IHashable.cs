using System;

namespace Nyris.Crdt.Distributed
{
    public interface IHashable
    {
        ReadOnlySpan<byte> GetHash();
    }
}