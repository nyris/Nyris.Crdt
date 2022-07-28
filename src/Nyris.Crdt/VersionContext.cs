using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Nyris.Crdt
{
    [DebuggerDisplay("{DebuggerDisplayString,nq}")]
    public sealed class VersionContext<TActorId>
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
    {
        private readonly ConcurrentDictionary<TActorId, DotRangeList> _ranges = new();

        public ulong StorageSize
        {
            get
            {
                var result = (ulong) (Marshal.SizeOf<TActorId>() * _ranges.Count);
                foreach (var ranges in _ranges.Values)
                {
                    result += (ulong) (Marshal.SizeOf<DotRange>() * ranges.Count);
                }

                return result;
            }
        }
        public ICollection<TActorId> Actors => _ranges.Keys;

        public bool TryGetValue(TActorId actorId, [NotNullWhen(true)] out DotRangeList? ranges) 
            => _ranges.TryGetValue(actorId, out ranges);

        public void Merge(TActorId actorId, ulong other)
        {
            // TODO: is allocating closures better then using a lock?
            _ranges.AddOrUpdate(actorId, _ =>
            {
                var ranges = new DotRangeList();
                ranges.Merge(other);
                return ranges;
            }, (_, current) =>
            {
                current.Merge(other);
                return current;
            });
        }

        public void Merge(TActorId actorId, DotRange range)
        {
            _ranges.AddOrUpdate(actorId, _ =>
            {
                var ranges = new DotRangeList();
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
            var ranges = _ranges.GetOrAdd(actorId, _ => new DotRangeList());
            return ranges.GetNew();
        }

        public Dictionary<TActorId, DotRange[]> ToDictionary() 
            => _ranges.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        
        public Dictionary<TActorId, T> ToDictionary<T>(Func<KeyValuePair<TActorId, DotRangeList>, T> valueFunc) 
            => _ranges.ToDictionary(pair => pair.Key, valueFunc);

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