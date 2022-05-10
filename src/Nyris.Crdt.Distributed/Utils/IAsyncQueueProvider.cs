using Nyris.Crdt.Distributed.Model;
using System;

namespace Nyris.Crdt.Distributed.Utils;

public interface IAsyncQueueProvider
{
    IAsyncScheduler<DtoMessage<TDto>> GetQueue<TDto>(Type crdtType);
}
