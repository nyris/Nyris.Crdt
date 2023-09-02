using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Managed.Metadata;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed;

internal sealed partial class Cluster : INodeFailureObserver
{
    public async Task NodeFailureObservedAsync(NodeId nodeId, CancellationToken cancellationToken)
    {
        if (_thisNode.Id == nodeId)
        {
            _logger.LogCritical("This should not be reachable - \"detected\" remote failure in itself, no-op");
            return;
        }

        var traceId = $"{_thisNode.Id}-observed-{nodeId}-fail";
        var context = new OperationContext(_thisNode.Id, -1, traceId, cancellationToken);
        ImmutableArray<NodeInfoSet.DeltaDto> nodesDeltas;
        var infosDeltas = new List<ImmutableArray<CrdtInfos.DeltaDto>>(_crdtInfos.Count);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var nodeInfo = _nodeSet.Values.FirstOrDefault(ni => ni.Id == nodeId);
            if (nodeInfo is null) return;

            _logger.LogInformation("TraceId '{TraceId}': Failure in node '{NodeId}' detected, removing from NodeSet",
                traceId, nodeId);
            nodesDeltas = _nodeSet.Remove(nodeInfo);
            foreach (var replicaId in _crdtInfos.Keys)
            {
                if (_crdtInfos.TryRemoveNodeAsHolderOfReadReplica(_thisNode.Id, nodeId, replicaId, out var infoDeltas))
                {
                    infosDeltas.Add(infoDeltas);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        await PropagateNodeDeltasAsync(nodesDeltas, _nodeSet.Values.ToImmutableArray(), context);
        await PropagateInfosAsync(infosDeltas.SelectMany(deltas => deltas), context);
        await DistributeShardsAsync(cancellationToken);
    }
}