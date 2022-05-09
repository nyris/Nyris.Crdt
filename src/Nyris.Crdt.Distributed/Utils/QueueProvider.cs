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

        public IAsyncScheduler<DtoMessage<TDto>> GetQueue<TDto>(Type crdtType) => (IAsyncScheduler<DtoMessage<TDto>>) _queueDictionary
            .GetOrAdd(crdtType, _ => new AsyncScheduler<DtoMessage<TDto>>(_queueCapacity));
    }
}
