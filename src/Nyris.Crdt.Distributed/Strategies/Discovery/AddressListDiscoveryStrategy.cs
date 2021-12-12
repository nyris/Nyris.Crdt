using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies.Discovery
{
    public sealed class AddressListDiscoveryStrategy : IDiscoveryStrategy
    {
        private readonly List<Uri> _addresses;

        public AddressListDiscoveryStrategy(List<Uri> addresses)
        {
            _addresses = addresses;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<NodeCandidate> GetNodeCandidates([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var nodeCandidate in _addresses.Select(uri => new NodeCandidate(uri, uri.ToString())))
            {
                yield return nodeCandidate;
            }
        }
    }
}