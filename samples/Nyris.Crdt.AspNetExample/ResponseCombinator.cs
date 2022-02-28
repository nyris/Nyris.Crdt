using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;

namespace Nyris.Crdt.AspNetExample
{
    public class ResponseCombinator : DefaultResponseCombinator
    {
        public override TResponse Combine<TResponse>(IEnumerable<TResponse> responses) where TResponse : class
        {
            if (typeof(TResponse) == typeof(ValueResponse<IList<ImageGuid>>))
            {
                var ids = new List<ImageGuid>();
                foreach (var response in responses)
                {
                    ids.AddRange((response as ValueResponse<IList<ImageGuid>>)?.Value ?? ArraySegment<ImageGuid>.Empty);
                }

                return (new ValueResponse<IList<ImageGuid>>(ids) as TResponse)!;
            }

            return base.Combine(responses);
        }
    }
}