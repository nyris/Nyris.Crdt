using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Discovery.Abstractions;

public interface IDiscoveryStrategy
{
    IAsyncEnumerable<NodeCandidate> GetNodeCandidates(CancellationToken cancellationToken = default);
}
