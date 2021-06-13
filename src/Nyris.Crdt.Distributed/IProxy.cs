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
        Task<TDto> SendAsync(WithId<TDto> dto);

        // [OperationContract]
        // IAsyncEnumerable<WithId<TDto>> EnumerateCrdtAsync(TypeNameAndInstanceId nameAndInstanceId);
    }
}