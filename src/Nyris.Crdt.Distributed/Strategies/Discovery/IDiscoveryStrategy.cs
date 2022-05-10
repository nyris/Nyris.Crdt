using Nyris.Crdt.Distributed.Model;
using System.Collections.Generic;
using System.Threading;

namespace Nyris.Crdt.Distributed.Strategies.Discovery;

public interface IDiscoveryStrategy
{
    IAsyncEnumerable<NodeCandidate> GetNodeCandidates(CancellationToken cancellationToken = default);
}
