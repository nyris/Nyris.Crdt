using System.Collections.Generic;
using System.Threading;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies
{
    public interface IDiscoveryStrategy
    {
        IAsyncEnumerable<NodeCandidate> GetNodeCandidates(CancellationToken cancellationToken = default);
    }
}