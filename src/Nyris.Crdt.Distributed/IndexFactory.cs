using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Nyris.Crdt.Distributed
{
    public static class IndexFactory
    {
        private static readonly ConcurrentDictionary<string, Func<object>> Factories = new();

        public static void Register(string indexName, Func<object> factory)
        {
            Factories.TryAdd(indexName, factory);
        }

        public static bool TryGetIndex<TIndex>(string indexName, [NotNullWhen(true)] out TIndex? index)
            where TIndex : class
        {
            if (!Factories.TryGetValue(indexName, out var factory))
            {
                index = default;
                return false;
            }

            index = factory() as TIndex;
            return index != null;
        }
    }
}