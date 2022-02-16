using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public interface IAcceptOperations<in TOperation, TOperationResponse>
        where TOperation : Operation
        where TOperationResponse : OperationResponse
    {
        Task<TOperationResponse> ApplyAsync(TOperation operation, CancellationToken cancellationToken = default);
    }
}