using System;
using System.Linq;
using Nyris.Crdt.Distributed.Utils;

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

        internal HashableLastWriteWinsRegistry(LastWriteWinsRegistryDto dto) : base(dto)
        {
        }

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_items.OrderBy(i => i.Value.TimeStamp));
    }
}