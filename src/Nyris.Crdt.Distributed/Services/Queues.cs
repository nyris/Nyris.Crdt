using System;
using System.Collections.Concurrent;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Services
{
    internal static class Queues
    {
        private static readonly ConcurrentDictionary<Type, object> QueueDictionary = new();

        public static AsyncQueue<WithId<TDto>> GetQueue<TDto>(Type crdtType)
        {
            return (AsyncQueue<WithId<TDto>>) QueueDictionary
                .GetOrAdd(crdtType, _ => new AsyncQueue<WithId<TDto>>());
        }
    }
}
