using System;
using System.Linq;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class HashableLastWriteWinsRegistry<TKey, TValue, TTimeStamp>
        : LastWriteWinsRegistry<TKey, TValue, TTimeStamp>, IHashable
        where TKey : IEquatable<TKey>
        where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
        where TValue : IHashable
    {
        public HashableLastWriteWinsRegistry()
        {
        }

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(Items.OrderBy(i => i.Value.TimeStamp));
    }
}
