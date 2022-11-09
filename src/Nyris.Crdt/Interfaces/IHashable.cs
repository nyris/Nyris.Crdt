using System;

namespace Nyris.Crdt.Interfaces;

public interface IHashable
{
    ReadOnlySpan<byte> CalculateHash();
}
