using System;
using System.Collections.Generic;
using System.Linq;

namespace Nyris.Crdt.Distributed.Extensions;

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

    public static bool OrderedEquals<TKey, TValue>(
        this IDictionary<TKey, IList<TValue>> first,
        IDictionary<TKey, IList<TValue>> second
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>, IComparable<TValue>
    {
        if (first.Count != second.Count) return false;

        foreach (var (key, value) in first)
        {
            if (!second.ContainsKey(key)) return false;

            if (!value.OrderBy(i => i).SequenceEqual(second[key].OrderBy(i => i))) return false;
        }

        return true;
    }
}
