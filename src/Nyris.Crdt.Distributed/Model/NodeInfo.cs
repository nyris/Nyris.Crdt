
using System;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record NodeInfo([property: ProtoMember(1)] Uri Address, [property: ProtoMember(2)] NodeId Id);
}
