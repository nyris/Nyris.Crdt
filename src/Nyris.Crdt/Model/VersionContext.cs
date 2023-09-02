using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Nyris.Crdt.Model
{
    [DebuggerDisplay("{DebuggerDisplayString,nq}")]
    public sealed class VersionContext<TActorId>
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
    {
        private readonly ConcurrentDictionary<TActorId, ConcurrentVersionRanges> _ranges = new();

        public ulong StorageSize
        {
            get
            {
                var result = (ulong) (Marshal.SizeOf<TActorId>() * _ranges.Count);
                foreach (var ranges in _ranges.Values)
                {
                    result += (ulong) (Marshal.SizeOf<Range>() * ranges.Count);
                }

                return result;
            }
        }
        public ICollection<TActorId> Actors => _ranges.Keys;

        public bool TryGetValue(TActorId actorId, [NotNullWhen(true)] out ConcurrentVersionRanges? ranges)
            => _ranges.TryGetValue(actorId, out ranges);

        public ConcurrentVersionRanges GetOrAdd(TActorId actorId) => _ranges.GetOrAdd(actorId, _ => new ConcurrentVersionRanges());

        public void Merge(TActorId actorId, ulong other)
        {
            // TODO: is allocating closures better then using a lock?
            _ranges.AddOrUpdate(actorId, _ =>
            {
                var ranges = new ConcurrentVersionRanges();
                ranges.Merge(other);
                return ranges;
            }, (_, current) =>
            {
                current.Merge(other);
                return current;
            });
        }

        public void Merge(TActorId actorId, Range range)
        {
            _ranges.AddOrUpdate(actorId, _ =>
            {
                var ranges = new ConcurrentVersionRanges();
                ranges.Merge(range);
                return ranges;
            }, (_, current) =>
            {
                current.Merge(range);
                return current;
            });

        }

        public ulong Increment(TActorId actorId)
        {
            var ranges = _ranges.GetOrAdd(actorId, _ => new ConcurrentVersionRanges());
            return ranges.GetNew();
        }

        public Dictionary<TActorId, Range[]> ToDictionary()
            => _ranges.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

        public ImmutableDictionary<TActorId, T> ToDictionary<T>(Func<KeyValuePair<TActorId, ConcurrentVersionRanges>, T> valueFunc)
            => _ranges.ToImmutableDictionary(pair => pair.Key, valueFunc);

        private string DebuggerDisplayString
        {
            get
            {
                var stringBuilder = new StringBuilder();
                foreach (var (actorId, ranges) in _ranges)
                {
                    var actorString = actorId.ToString()![..6];
                    stringBuilder.Append(actorString).Append(": ");
                    foreach (var range in ranges.ToArray())
                    {
                        stringBuilder.Append(range.ToString()).Append(", ");
                    }

                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                    stringBuilder.Append("; ");
                }

                return stringBuilder.ToString();
            }
        }
    }
}