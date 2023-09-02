using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Managed.Discovery.Abstractions;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.Services.Hosted;

internal sealed class DiscoveryService : BackgroundService
{
    private readonly ICollection<IDiscoveryStrategy> _strategies;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly NodeInfo _thisNode;
    private readonly IClusterMetadataManager _clusterMetadata;
    private readonly INodeClientFactory _nodeClientFactory;

    /// <inheritdoc />
    public DiscoveryService(IEnumerable<IDiscoveryStrategy> strategies,
        ILogger<DiscoveryService> logger,
        NodeInfo thisNode,
        IClusterMetadataManager clusterMetadata,
        INodeClientFactory nodeClientFactory)
    {
        _strategies = strategies.ToList();
        _logger = logger;
        _thisNode = thisNode;
        _clusterMetadata = clusterMetadata;
        _nodeClientFactory = nodeClientFactory;
        _logger.LogInformation("Initialized discovery service with {StrategyCount} strategies", _strategies.Count);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var traceId = $"{_thisNode.Id}-discovery";
        var context = new OperationContext(_thisNode.Id, -1, traceId, stoppingToken);
        try
        {
            await TryExecutingAsync(context);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TraceId '{TraceId}': Unhandled exception in {ServiceName}",
                traceId, nameof(DiscoveryService));
        }
    }

    private async Task TryExecutingAsync(OperationContext context)
    {
        _logger.LogDebug("TraceId '{TraceId}': {ServiceName} executing",
             context.TraceId, nameof(DiscoveryService));

        await foreach (var (address, name) in GetAllUris(context.CancellationToken))
        {
            try
            {
                await TryConnectingToNodeAsync(address, name, context);
            }
            catch (Exception e)
            {
                _logger.LogError("TraceId '{TraceId}': Connecting to node {PodName} failed due to an exception: {ExceptionMessage}",
                    context.TraceId, name, e.ToString());
            }
        }

        _logger.LogDebug("TraceId '{TraceId}': Discovery completed", context.TraceId);
    }

    private async Task TryConnectingToNodeAsync(Uri address, string name, OperationContext context)
    {
        _logger.LogDebug("TraceId '{TraceId}': Attempting to connect to {NodeName} at {NodeAddress}",
            context.TraceId, name, address);

        var client = _nodeClientFactory.GetClientForNodeCandidate(address);
        await foreach (var (kind, dto) in client.JoinToClusterAsync(_thisNode, context))
        {
            await _clusterMetadata.MergeAsync(kind, dto, context);
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