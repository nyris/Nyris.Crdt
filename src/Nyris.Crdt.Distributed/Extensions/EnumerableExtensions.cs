using System;
using System.Collections.Generic;
using System.Linq;

namespace Nyris.Crdt.Distributed.Extensions
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<ReadOnlyMemory<T>> Batch<T>(this ICollection<T> source, int size)
        {
            if (source.Count <= size)
            {
                yield return new ReadOnlyMemory<T>(source.ToArray());
                yield break;
            }

            var bucket = new Memory<T>(new T[size]);
            var count = 0;

            foreach (var item in source)
            {
                bucket.Span[count++] = item;
                if (count < size) continue;

                yield return bucket;
                count = 0;
            }

            // if we happen to have integer number of batches, return
            if (count == 0) yield break;

            yield return bucket[..count];
        }
    }
}