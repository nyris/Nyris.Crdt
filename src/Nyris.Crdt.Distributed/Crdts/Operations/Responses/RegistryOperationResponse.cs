using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Crdts.Operations.Responses;

public abstract record RegistryOperationResponse : OperationResponse;

public interface IResponseCombinator
{
    TResponse Combine<TResponse>(IEnumerable<TResponse> responses) where TResponse : class;
}
