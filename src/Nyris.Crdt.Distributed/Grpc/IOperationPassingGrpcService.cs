using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Grpc
{
    [ServiceContract]
    public interface IOperationPassingGrpcService<TOperation, TKey>
    {
        [OperationContract]
        Task ApplyAsync(CrdtOperation<TOperation, TKey> operation, CancellationToken cancellationToken = default);
    }
}