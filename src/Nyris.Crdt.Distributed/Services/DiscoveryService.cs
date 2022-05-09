using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Discovery;
using Nyris.Extensions.Guids;
using ProtoBuf.Grpc.Client;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class DiscoveryService<TGrpcService> : BackgroundService
        where TGrpcService : class
    {
        private readonly ManagedCrdtContext _context;
        private readonly ICollection<IDiscoveryStrategy> _strategies;
        private readonly ILogger<DiscoveryService<TGrpcService>> _logger;
        private readonly NodeInfo _thisNode;
        private readonly GrpcChannelOptions _grpcChannelOptions;

        /// <inheritdoc />
        public DiscoveryService(
            ManagedCrdtContext context,
            IEnumerable<IDiscoveryStrategy> strategies,
            ILogger<DiscoveryService<TGrpcService>> logger,
            NodeInfo thisNode
        )
        {
            _strategies = strategies.ToList();
            _logger = logger;
            _thisNode = thisNode;
            _context = context;
            _grpcChannelOptions = new GrpcChannelOptions
            {
                ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig
                        {
                            Names = { MethodName.Default },
                            RetryPolicy = new RetryPolicy
                            {
                                MaxAttempts = 5,
                                InitialBackoff = TimeSpan.FromSeconds(2),
                                BackoffMultiplier = 2,
                                MaxBackoff = TimeSpan.FromSeconds(32),
                                RetryableStatusCodes = { StatusCode.Unavailable }
                            }
                        }
                    }
                }
            };

            _logger.LogInformation("Initialized discovery service with {StrategyCount} strategies", _strategies.Count);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await TryExecutingAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception in {ServiceName}",
                                 nameof(DiscoveryService<TGrpcService>));
            }
        }

        private async Task TryExecutingAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("{ServiceName} executing", nameof(DiscoveryService<TGrpcService>));

            await _context.Nodes.AddAsync(_thisNode);
            await foreach (var (address, name) in GetAllUris(cancellationToken))
            {
                try
                {
                    await TryConnectingToNodeAsync(address, name);
                }
                catch (Exception e)
                {
                    _logger.LogError("Connecting to node {PodName} failed due to an exception: {ExceptionMessage}",
                                     name, e.ToString());
                }
            }

            _logger.LogDebug("Discovery completed");
        }

        private async Task TryConnectingToNodeAsync(Uri address, string name)
        {
            _logger.LogDebug("Attempting to connect to {NodeName} at {NodeAddress}", name, address);
            using var channel = GrpcChannel.ForAddress(address, _grpcChannelOptions);

            if (channel.CreateGrpcService<TGrpcService>() is not IDtoPassingGrpcService<NodeSet.NodeSetDto> proxy)
            {
                throw new InitializationException(
                                                  $"Internal error: specified {nameof(TGrpcService)} does not implement IProxy<NodeSet.Dto>");
            }

            var dto = await _context.Nodes.ToDtoAsync();
            using var msg = new DtoMessage<NodeSet.NodeSetDto>(_context.Nodes.TypeName,
                                                               _context.Nodes.InstanceId,
                                                               dto, ShortGuid.Encode(Guid.NewGuid()));
            var response = await proxy.SendAsync(msg);

            _logger.LogDebug(
                             "Received a NodeSet dto from {NodeName} with {ItemCount} items and {NodeCount} known nodes",
                             name, response.Items?.Count, response.VersionVectors?.Count);

            await _context.MergeAsync<NodeSet, NodeSet.NodeSetDto>(response, _context.Nodes.InstanceId);

            _logger.LogDebug("State after merging: {NodeList}", string.Join(", ",
                                                                            _context.Nodes.Value.Select(ni => $"{ni.Id}:{ni.Address}")));
        }

        private async IAsyncEnumerable<NodeCandidate> GetAllUris(
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            var set = new HashSet<NodeCandidate>();

            foreach (var strategy in _strategies)
            {
                _logger.LogInformation("Executing {StrategyName} discovery strategy", strategy.GetType().Name);
                await foreach (var address in strategy.GetNodeCandidates(cancellationToken))
                {
                    if (set.Contains(address)) continue;

                    _logger.LogInformation(
                                           "Strategy yielded candidate {NodeCandidateName} at address '{NodeCandidateAddress}'",
                                           address.Name, address.Address);

                    set.Add(address);
                    yield return address;
                }
            }

            if (set.Count == 0)
                _logger.LogWarning(
                                   "Discovery strategies yielded no node candidates. Did you add discovery strategies?");
        }
    }
}
