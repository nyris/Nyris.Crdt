using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Discovery;

namespace Nyris.ManagedCrdtsV2;

internal sealed class DiscoveryService : BackgroundService
{
    private readonly ICollection<IDiscoveryStrategy> _strategies;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly NodeInfo _thisNode;
    private readonly IClusterMetadataManager _clusterMetadata;
    private readonly INodeClientPool _nodeClientPool;

    /// <inheritdoc />
    public DiscoveryService(IEnumerable<IDiscoveryStrategy> strategies,
        ILogger<DiscoveryService> logger,
        NodeInfo thisNode, 
        IClusterMetadataManager clusterMetadata, 
        INodeClientPool nodeClientPool)
    {
        _strategies = strategies.ToList();
        _logger = logger;
        _thisNode = thisNode;
        _clusterMetadata = clusterMetadata;
        _nodeClientPool = nodeClientPool;
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
            _logger.LogError(e, "Unhandled exception in {ServiceName}", nameof(DiscoveryService));
        }
    }

    private async Task TryExecutingAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("{ServiceName} executing", nameof(DiscoveryService));

        await foreach (var (address, name) in GetAllUris(cancellationToken))
        {
            try
            {
                await TryConnectingToNodeAsync(address, name, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError("Connecting to node {PodName} failed due to an exception: {ExceptionMessage}",
                    name, e.ToString());
            }
        }

        _logger.LogDebug("Discovery completed");
    }

    private async Task TryConnectingToNodeAsync(Uri address, string name, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Attempting to connect to {NodeName} at {NodeAddress}", name, address);

        var client = _nodeClientPool.GetClientForNodeCandidate(address);
        var dto = await client.JoinToClusterAsync(_thisNode, cancellationToken);

        if (dto.IsEmpty)
        {
            _logger.LogWarning("Node {NodeName} returned null as dto for NodeSet", name);
        }
        else
        {
            await _clusterMetadata.MergeAsync(MetadataDto.NodeSetFull, dto, cancellationToken);
        }
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