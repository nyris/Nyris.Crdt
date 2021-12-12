using System;
using System.Collections.Concurrent;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Services
{
    internal static class Queues
    {
        private static readonly ConcurrentDictionary<Type, object> QueueDictionary = new();

        public static AsyncQueue<DtoMessage<TDto>> GetQueue<TDto>(Type crdtType)
        {
            return (AsyncQueue<DtoMessage<TDto>>) QueueDictionary
                .GetOrAdd(crdtType, _ => new AsyncQueue<DtoMessage<TDto>>());
        }
    }
}
