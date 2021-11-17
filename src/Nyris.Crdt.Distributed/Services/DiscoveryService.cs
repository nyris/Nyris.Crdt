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
using Nyris.Crdt.Distributed.Strategies.Discovery;
using ProtoBuf.Grpc.Client;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class DiscoveryService<TGrpcService> : BackgroundService
        where TGrpcService : class
    {
        private readonly ManagedCrdtContext _context;
        private readonly IEnumerable<IDiscoveryStrategy> _strategies;
        private readonly ILogger<DiscoveryService<TGrpcService>> _logger;
        private readonly NodeInfo _thisNode;

        /// <inheritdoc />
        public DiscoveryService(ManagedCrdtContext context,
            IEnumerable<IDiscoveryStrategy> strategies,
            ILogger<DiscoveryService<TGrpcService>> logger,
            NodeInfo thisNode)
        {
            _strategies = strategies;
            _logger = logger;
            _thisNode = thisNode;
            _context = context;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("{ServiceName} executing", nameof(DiscoveryService<TGrpcService>));

            await _context.Nodes.AddAsync(_thisNode, _thisNode.Id);
            await foreach (var (address, name) in GetAllUris(stoppingToken))
            {
                _logger.LogDebug("Attempting to connect to {NodeName} at {NodeAddress}", name, address);
                using var channel = GrpcChannel.ForAddress(address);

                if (channel.CreateGrpcService<TGrpcService>() is not IProxy<NodeSet.OrSetDto> proxy)
                {
                    throw new InitializationException($"Internal error: specified {nameof(TGrpcService)} does not implement IProxy<NodeSet.Dto>");
                }

                var dto = await _context.Nodes.ToDtoAsync();
                var response = await proxy.SendAsync(dto.WithId(_context.Nodes.InstanceId));
                await _context.MergeAsync<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>,
                    HashSet<NodeInfo>, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>.OrSetDto>(
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