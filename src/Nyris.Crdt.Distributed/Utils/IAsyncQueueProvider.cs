using System;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils
{
    public interface IAsyncQueueProvider
    {
        IAsyncScheduler<DtoMessage<TDto>> GetQueue<TDto>(Type crdtType);
    }
}
