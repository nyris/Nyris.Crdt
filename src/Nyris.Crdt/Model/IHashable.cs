using System;

namespace Nyris.Crdt.Model
{
    public interface IHashable
    {
        ReadOnlySpan<byte> CalculateHash();
    }
}
