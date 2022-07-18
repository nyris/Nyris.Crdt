using System;
using System.Diagnostics;

namespace Nyris.Crdt
{
    [DebuggerDisplay("[{From}, {To})")]
    public readonly struct DotRange : IEquatable<DotRange>
    {
        public readonly ulong From;
        public readonly ulong To;

        /// <summary>
        /// A continuous range of dots
        /// </summary>
        /// <param name="from">Inclusive</param>
        /// <param name="to">Exclusive</param>
        public DotRange(ulong from, ulong to)
        {
            Debug.Assert(to > from);
            From = from;
            To = to;
        }

        public override string ToString() => $"[{From}, {To})";
        public bool Equals(DotRange other) => From == other.From && To == other.To;
        public override bool Equals(object? obj) => obj is DotRange other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(From, To);
        public static bool operator ==(DotRange left, DotRange right) => left.Equals(right);
        public static bool operator !=(DotRange left, DotRange right) => !(left == right);
    }
}
