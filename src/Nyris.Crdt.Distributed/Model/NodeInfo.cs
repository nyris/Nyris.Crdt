using System;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record NodeInfo([property: ProtoMember(1)] Uri Address,
        [property: ProtoMember(2)] NodeId Id) : IHashable
    {
        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(Address, Id);
    }
}
