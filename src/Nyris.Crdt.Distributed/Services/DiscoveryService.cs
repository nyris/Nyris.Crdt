using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies;
using ProtoBuf.Grpc.Client;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class DiscoveryService<TGrpcService> : BackgroundService
        where TGrpcService : class
    {
        private readonly ManagedCrdtContext _context;
        private readonly IEnumerable<IDiscoveryStrategy> _strategies;
        private readonly ILogger<DiscoveryService<TGrpcService>> _logger;

        /// <inheritdoc />
        public DiscoveryService(ManagedCrdtContext context,
            IEnumerable<IDiscoveryStrategy> strategies,
            ILogger<DiscoveryService<TGrpcService>> logger)
        {
            _strategies = strategies;
            _logger = logger;
            _context = context;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var (address, name) in GetAllUris(stoppingToken))
            {
                _logger.LogDebug("Attempting to connect to {NodeName} at {NodeAddress}", name, address);
                using var channel = GrpcChannel.ForAddress(address);

                if (channel.CreateGrpcService<TGrpcService>() is not IProxy<NodeSet.Dto> proxy)
                {
                    throw new InitializationException($"Internal error: specified {nameof(TGrpcService)} does not implement IProxy<NodeSet.Dto>");
                }

                var response = await proxy.SendAsync(_context.Nodes.ToDto().WithId(_context.Nodes.InstanceId));
                _context.Merge<NodeSet, OptimizedObservedRemoveSet<NodeId, NodeInfo>,
                    HashSet<NodeInfo>, OptimizedObservedRemoveSet<NodeId, NodeInfo>.Dto>(
                    response.WithId(_context.Nodes.InstanceId));
            }
        }

        private async IAsyncEnumerable<NodeCandidate> GetAllUris([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var set = new HashSet<NodeCandidate>();

            foreach (var strategy in _strategies)
            {
                await foreach (var address in strategy.GetNodeCandidates(cancellationToken))
                {
                    if (set.Contains(address)) continue;

                    set.Add(address);
                    yield return address;
                }
            }

            if(!set.Any()) _logger.LogWarning("Discovery strategies yielded no node candidates. Did you add discovery strategies?");
        }
    }
}