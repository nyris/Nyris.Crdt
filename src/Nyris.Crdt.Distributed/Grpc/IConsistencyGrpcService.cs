using Nyris.Crdt.Distributed.Model;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Grpc;

[ServiceContract]
public interface IConsistencyGrpcService
{
    [OperationContract]
    Task<byte[]> GetHashAsync(PrimitiveWrapper<InstanceId> instanceId, CancellationToken cancellationToken = default);
}
