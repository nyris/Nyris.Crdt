using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Grpc
{
    [ServiceContract]
    public interface IOperationPassingGrpcService<TOperation, TOperationResult>
        where TOperation : Operation
        where TOperationResult : OperationResponse
    {
        [OperationContract]
        Task<Response<TOperationResult>> ApplyAsync(CrdtOperation<TOperation> operation, CancellationToken cancellationToken = default);
    }
}