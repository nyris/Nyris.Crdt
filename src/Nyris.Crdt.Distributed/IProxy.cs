using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed
{
    [ServiceContract]
    public interface IProxy<TDto> : IConsistencyGrpcService
    {
        [OperationContract]
        Task<TDto> SendAsync(WithId<TDto> dto);

        [OperationContract]
        IAsyncEnumerable<WithId<TDto>> EnumerateCrdtAsync(IAsyncEnumerable<WithId<TDto>> dtos);
        // , TypeNameAndInstanceId nameAndInstanceId
    }
}