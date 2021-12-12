using System;
using System.Collections.Concurrent;
using Nyris.Extensions.Guids;

namespace Nyris.Crdt.Distributed.Utils
{
    public static class TypeNameCompressor
    {
        private static readonly ConcurrentDictionary<Type, string> Dict = new();

        public static string GetName(Type t)
            => Dict.GetOrAdd(t, type => ShortGuid.Encode(Guid5.Create(type.FullName ?? type.Name)));
        public static string GetName<T>() => GetName(typeof(T));
    }
}