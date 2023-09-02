using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Strategies.NodeSelection;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.Services.Propagation;

internal sealed class MetadataPropagationService : IMetadataPropagationService
{
    private readonly INodeSubsetSelectionStrategy _selectionStrategy;
    private readonly INodeClientFactory _clientFactory;

    public MetadataPropagationService(INodeSubsetSelectionStrategy selectionStrategy, INodeClientFactory clientFactory)
    {
        _selectionStrategy = selectionStrategy;
        _clientFactory = clientFactory;
    }

    public async Task PropagateAsync(MetadataKind kind,
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