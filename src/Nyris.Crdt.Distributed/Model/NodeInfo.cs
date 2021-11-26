using System;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record NodeInfo([property: ProtoMember(1)] Uri Address,
        [property: ProtoMember(2)] NodeId Id) : IHashable
    {
        /// <inheritdoc />
        public ReadOnlySpan<byte> GetHash() => HashingHelper.Combine(Address, Id);
    }
}
