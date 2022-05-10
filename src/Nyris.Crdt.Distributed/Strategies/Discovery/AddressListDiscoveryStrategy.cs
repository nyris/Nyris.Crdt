using Nyris.Crdt.Distributed.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nyris.Crdt.Distributed.Strategies.Discovery;

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
