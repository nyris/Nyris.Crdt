using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf.Grpc;

namespace Nyris.Crdt.Distributed.Grpc
{
    [ServiceContract]
    public interface IDtoPassingGrpcService<TDto> : IConsistencyGrpcService
    {
        [OperationContract]
        Task<TDto> SendAsync(DtoMessage<TDto> dto, CancellationToken cancellationToken = default);

        [OperationContract]
        IAsyncEnumerable<TDto> EnumerateCrdtAsync(IAsyncEnumerable<TDto> dtos, CallContext context = default);
    }


}