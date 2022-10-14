using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;
using Nyris.ManagedCrdtsV2.Strategies.NodeSelection;

namespace Nyris.ManagedCrdtsV2.Services;

internal sealed class MetadataPropagationService : IMetadataPropagationService
{
    private readonly INodeSelectionStrategy _selectionStrategy;
    private readonly INodeClientFactory _clientFactory;

    public MetadataPropagationService(INodeSelectionStrategy selectionStrategy, INodeClientFactory clientFactory)
    {
        _selectionStrategy = selectionStrategy;
        _clientFactory = clientFactory;
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
            
            var client = _clientFactory.GetClient(nodeInfo);
            await client.MergeMetadataAsync(kind, data, context);
        }
    }
}