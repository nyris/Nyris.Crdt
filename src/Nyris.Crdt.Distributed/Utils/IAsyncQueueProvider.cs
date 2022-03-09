using System;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils
{
    public interface IAsyncQueueProvider
    {
        IAsyncQueue<DtoMessage<TDto>> GetQueue<TDto>(Type crdtType);
    }
}