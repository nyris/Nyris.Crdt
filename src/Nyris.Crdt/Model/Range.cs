using System;
using System.Diagnostics;

namespace Nyris.Crdt.Model
{
    [DebuggerDisplay("[{From}, {To})")]
    public readonly struct Range : IEquatable<Range>
    {
        public readonly ulong From;
        public readonly ulong To;

        /// <summary>
        /// A continuous range of dots
        /// </summary>
        /// <param name="from">Inclusive</param>
        /// <param name="to">Exclusive</param>
        public Range(ulong from, ulong to)
        {
            Debug.Assert(to > from);
            From = from;
            To = to;
        }

        public override string ToString() => $"[{From}, {To})";
        public bool Equals(Range other) => From == other.From && To == other.To;
        public override bool Equals(object? obj) => obj is Range other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(From, To);
        public static bool operator ==(Range left, Range right) => left.Equals(right);
        public static bool operator !=(Range left, Range right) => !(left == right);

        public void Deconstruct(out ulong from, out ulong to)
        {
            from = From;
            to = To;
        }
    }
}
