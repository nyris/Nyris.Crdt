using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Grpc
{
    [ServiceContract]
    public interface IOperationPassingGrpcService<TOperation, TOperationResult, TKey>
    {
        [OperationContract]
        Task<TOperationResult> ApplyAsync(CrdtOperation<TOperation, TKey> operation, CancellationToken cancellationToken = default);
    }
}