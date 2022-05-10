using System;

namespace Nyris.Crdt.Distributed.Model
{
    public sealed record NodeCandidate(Uri Address, string Name);
}
