using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using System.Collections.Generic;
using System.Linq;

namespace Nyris.Crdt.Distributed
{
    public class DefaultResponseCombinator : IResponseCombinator
    {
        /// <inheritdoc />
        public virtual TResponse Combine<TResponse>(IEnumerable<TResponse> responses) where TResponse : class
            => responses.First();
    }
}
