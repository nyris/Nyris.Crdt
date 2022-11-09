using System;
using System.Collections.Generic;

namespace Nyris.Crdt.Model
{
    public readonly struct DottedItem<TItem> : IEquatable<DottedItem<TItem>>
    {
        public readonly ulong Dot;
        public readonly TItem Item;

        public DottedItem(TItem item, ulong dot)
        {
            Item = item;
            Dot = dot;
        }

        public bool Equals(DottedItem<TItem> other) => Dot == other.Dot && EqualityComparer<TItem>.Default.Equals(Item, other.Item);
        public override bool Equals(object? obj) => obj is DottedItem<TItem> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Dot, Item);
        public static bool operator ==(DottedItem<TItem> left, DottedItem<TItem> right) => left.Equals(right);
        public static bool operator !=(DottedItem<TItem> left, DottedItem<TItem> right) => !(left == right);

        public void Deconstruct(out TItem item, out ulong dot)
        {
            item = Item;
            dot = Dot;
        }
    }
}
