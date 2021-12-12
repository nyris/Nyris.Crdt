using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Grpc
{
    [ServiceContract]
    public interface IConsistencyGrpcService
    {
        [OperationContract]
        Task<byte[]> GetHashAsync(TypeNameAndInstanceId nameAndInstanceId, CancellationToken cancellationToken = default);
    }
}