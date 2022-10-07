using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

internal sealed class MetadataPropagationService : IMetadataPropagationService
{
    private readonly INodeSelectionStrategy _selectionStrategy;
    private readonly INodeClientPool _clientPool;

    public MetadataPropagationService(INodeSelectionStrategy selectionStrategy, INodeClientPool clientPool)
    {
        _selectionStrategy = selectionStrategy;
        _clientPool = clientPool;
    }

    public async Task PropagateAsync(MetadataDto kind,
        ReadOnlyMemory<byte> data,
        IReadOnlyCollection<NodeInfo> nodesInCluster,
        CancellationToken cancellationToken = default)
    {
        var targetNodes = _selectionStrategy.SelectNodes(nodesInCluster);
        foreach (var nodeInfo in targetNodes)
        {
            var client = _clientPool.GetClient(nodeInfo);
            await client.MergeMetadataAsync(kind, data, cancellationToken);
        }
    }
}