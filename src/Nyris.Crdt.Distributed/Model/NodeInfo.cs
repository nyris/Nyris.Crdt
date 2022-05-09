using System;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record NodeInfo(
        [property: ProtoMember(1)] Uri Address,
        [property: ProtoMember(2)] NodeId Id
    ) : IHashable, IComparable<NodeInfo>
    {
        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(Address, Id);

        /// <inheritdoc />
        public int CompareTo(NodeInfo? other)
        {
            if (ReferenceEquals(this, other)) return 0;

            return other is null ? 1 : Id.CompareTo(other.Id);
        }

        public static bool operator <(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) < 0;

        public static bool operator <=(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) <= 0;

        public static bool operator >(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) > 0;

        public static bool operator >=(NodeInfo? left, NodeInfo? right) => left?.CompareTo(right) >= 0;
    }
}
