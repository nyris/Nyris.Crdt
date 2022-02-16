using System;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{

    public interface IHashable
    {
        ReadOnlySpan<byte> CalculateHash();
    }
}