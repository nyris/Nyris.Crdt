using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using System;
using System.Collections.Generic;

namespace Nyris.Crdt.AspNetExample;

public class ResponseCombinator : DefaultResponseCombinator
{
    private readonly ILogger<ResponseCombinator> _logger;

    public ResponseCombinator(ILogger<ResponseCombinator> logger)
    {
        _logger = logger;
    }

    public override TResponse Combine<TResponse>(IEnumerable<TResponse> responses)
    {
        if (typeof(TResponse) != typeof(ValueResponse<IList<ImageGuid>>))
            return base.Combine(responses);

        _logger.LogDebug("Responses are a list of ImageGuids and will be combined");
        var ids = new List<ImageGuid>();
        foreach (var response in responses)
        {
            ids.AddRange((response as ValueResponse<IList<ImageGuid>>)?.Value ?? ArraySegment<ImageGuid>.Empty);
        }

        return (new ValueResponse<IList<ImageGuid>>(ids) as TResponse)!;
    }
}
