using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;

namespace Nyris.Crdt.Distributed
{
    [ServiceContract]
    public interface IProxy<TDto> : IConsistencyGrpcService
    {
        [OperationContract]
        // ReSharper disable once OperationContractWithoutServiceContract - service contract is generated by SourceGenerator
        Task<TDto> SendAsync(WithId<TDto> dto);

        [OperationContract]
        IAsyncEnumerable<WithId<TDto>> EnumerateCrdtAsync(TypeNameAndInstanceId nameAndInstanceId);
    }
}