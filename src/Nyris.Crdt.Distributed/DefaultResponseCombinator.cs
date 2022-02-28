using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;

namespace Nyris.Crdt.Distributed
{
    public class DefaultResponseCombinator : IResponseCombinator
    {
        /// <inheritdoc />
        public virtual TResponse Combine<TResponse>(IEnumerable<TResponse> responses) where TResponse : class
            => responses.First();
    }
}