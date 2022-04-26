using System;
using System.Collections.Concurrent;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils
{
    internal sealed class QueueProvider : IAsyncQueueProvider
    {
        private readonly int _queueCapacity;

        private readonly ConcurrentDictionary<Type, object> _queueDictionary = new();

        public QueueProvider(int queueCapacity = 5)
        {
            _queueCapacity = queueCapacity;
        }

        public IAsyncQueue<DtoMessage<TDto>> GetQueue<TDto>(Type crdtType)
        {
            return (IAsyncQueue<DtoMessage<TDto>>) _queueDictionary
                .GetOrAdd(crdtType, _ => new AsyncQueue<DtoMessage<TDto>>(_queueCapacity));
        }
    }
}