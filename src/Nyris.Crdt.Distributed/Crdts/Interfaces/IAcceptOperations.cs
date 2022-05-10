using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces;

public interface IAcceptOperations<in TOperation, TOperationResponse>
    where TOperation : Operation
    where TOperationResponse : OperationResponse
{
    Task<TOperationResponse> ApplyAsync(TOperation operation, CancellationToken cancellationToken = default);
}
