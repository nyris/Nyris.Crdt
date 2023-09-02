using System.Runtime.CompilerServices;
using Nyris.Crdt.Managed.Discovery.Abstractions;
using Nyris.Crdt.Managed.Model;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Nyris.Crdt.Managed.Strategies.Discovery;

public sealed class AddressListDiscoveryStrategy : IDiscoveryStrategy
{
    private readonly IReadOnlyCollection<Uri> _addresses;

    public AddressListDiscoveryStrategy(IReadOnlyCollection<Uri> addresses)
    {
        _addresses = addresses;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NodeCandidate> GetNodeCandidates(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        foreach (var nodeCandidate in _addresses.Select(uri => new NodeCandidate(uri, uri.ToString())))
        {
            yield return nodeCandidate;
        }
    }
}
