using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class HashableGrowthSet<TItem> : GrowthSet<TItem>, IHashable
        where TItem : IEquatable<TItem>
    {
        public HashableGrowthSet()
        {
        }

        public HashableGrowthSet(IEnumerable<TItem> items) : base(items)
        {
        }

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(Set);
    }
}
