using System.Collections.Immutable;
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
        ImmutableArray<NodeInfo> nodesInCluster,
        OperationContext context)
    {
        var targetNodes = _selectionStrategy.SelectNodes(nodesInCluster);
        foreach (var nodeInfo in targetNodes)
        {
            if(nodeInfo.Id == context.Origin) continue;
            
            var client = _clientPool.GetClient(nodeInfo);
            await client.MergeMetadataAsync(kind, data, context);
        }
    }
}