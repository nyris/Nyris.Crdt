using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Managed.Metadata;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed;

internal sealed partial class Cluster : IClusterMetadataManager
{
    public async IAsyncEnumerable<(MetadataKind, ReadOnlyMemory<byte>)> AddNewNodeAsync(NodeInfo nodeInfo,
        OperationContext context)
    {
        // we don't want to accidentally propagate the update to a newly added node
        var nodesBeforeAddition = _nodeSet.Values.ToImmutableArray();
        var deltas = await AddNodeInfoAsync(nodeInfo, context.CancellationToken);
        await PropagateNodeDeltasAsync(deltas, nodesBeforeAddition, context);
        await DistributeShardsAsync(context.CancellationToken);

        foreach (var delta in _nodeSet.EnumerateDeltaDtos().Chunk(100))
        {
            yield return (MetadataKind.NodeSet, _serializer.Serialize(delta));
        }
        foreach (var delta in _crdtConfigs.EnumerateDeltaDtos().Chunk(100))
        {
            yield return (MetadataKind.CrdtConfigs, _serializer.Serialize(delta));
        }
        foreach (var delta in _crdtInfos.EnumerateDeltaDtos().Chunk(100))
        {
            yield return (MetadataKind.CrdtInfos, _serializer.Serialize(delta));
        }
    }

    public async Task MergeAsync(MetadataKind kind, ReadOnlyMemory<byte> dto, OperationContext context)
    {
        var nodeSetBeforeMerge = _nodeSet.Values.ToImmutableArray();
        DeltaMergeResult result;

        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            result = MergeInternal(kind, dto, context.TraceId);
        }
        finally
        {
            _semaphore.Release();
        }

        if (result == DeltaMergeResult.StateUpdated)
        {
            // _logger.LogDebug("TraceId '{TraceId}': After merging {Kind} delta state was updated, propagating further", 
            //     context.TraceId, kind.ToString("G"));
            await _propagationService.PropagateAsync(kind, dto, nodeSetBeforeMerge, context);
            await DistributeShardsAsync(context.CancellationToken);
        }
    }
    
    
    public ImmutableDictionary<MetadataKind, ReadOnlyMemory<byte>> GetCausalTimestamps(
        CancellationToken cancellationToken)
    {
        var builder = ImmutableDictionary.CreateBuilder<MetadataKind, ReadOnlyMemory<byte>>();

        var serialized = _serializer.Serialize(_nodeSet.GetLastKnownTimestamp());
        builder.Add(MetadataKind.NodeSet, serialized);
        
        serialized = _serializer.Serialize(_crdtConfigs.GetLastKnownTimestamp());
        builder.Add(MetadataKind.CrdtConfigs, serialized);
        
        serialized = _serializer.Serialize(_crdtInfos.GetLastKnownTimestamp());
        builder.Add(MetadataKind.CrdtInfos, serialized);

        return builder.ToImmutable();
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltasAsync(MetadataKind kind, 
        ReadOnlyMemory<byte> timestamp, 
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case MetadataKind.NodeSet:
                var nodesTimestamp = _serializer.Deserialize<NodeInfoSet.CausalTimestamp>(timestamp);
                foreach (var deltaDto in _nodeSet.EnumerateDeltaDtos(nodesTimestamp).Chunk(100))
                {
                    yield return _serializer.Serialize(deltaDto);
                }
                break;
            case MetadataKind.CrdtInfos:
                var infosTimestamp = _serializer.Deserialize<CrdtInfos.CausalTimestamp>(timestamp);
                foreach (var deltaDto in _crdtInfos.EnumerateDeltaDtos(infosTimestamp).Chunk(100))
                {
                    yield return _serializer.Serialize(deltaDto);
                }
                break;
            case MetadataKind.CrdtConfigs:
                var configsTimestamp = _serializer.Deserialize<CrdtConfigs.CausalTimestamp>(timestamp);
                foreach (var deltaDto in _crdtConfigs.EnumerateDeltaDtos(configsTimestamp).Chunk(100))
                {
                    yield return _serializer.Serialize(deltaDto);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
    
    private async Task<ImmutableArray<NodeInfoSet.DeltaDto>> AddNodeInfoAsync(NodeInfo nodeInfo, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _nodeSet.Add(nodeInfo, nodeInfo.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private DeltaMergeResult MergeInternal(MetadataKind kind, ReadOnlyMemory<byte> dto, string traceId)
    {
        var result = DeltaMergeResult.StateNotChanged;
        switch (kind)
        {
            case MetadataKind.NodeSet:
                var nodeDeltas = _serializer.Deserialize<ImmutableArray<NodeInfoSet.DeltaDto>>(dto);
                // _logger.LogDebug("TraceId '{TraceId}': Received nodeSet deltas: {Deltas}", traceId, JsonConvert.SerializeObject(nodeDeltas));
                result = _nodeSet.Merge(nodeDeltas);
                break;
            case MetadataKind.CrdtInfos:
                var infoDeltas = _serializer.Deserialize<ImmutableArray<CrdtInfos.DeltaDto>>(dto);
                // _logger.LogDebug("TraceId '{TraceId}': Received info deltas: {Deltas}", traceId, JsonConvert.SerializeObject(infoDeltas));
                result = _crdtInfos.Merge(infoDeltas);

                // TODO: maybe implement callback in ObservedRemoveMap instead
                if (result is DeltaMergeResult.StateUpdated) EnsureAllReadReplicasMarkedLocally();
                break;
            case MetadataKind.CrdtConfigs:
                var configDeltas = _serializer.Deserialize<ImmutableArray<CrdtConfigs.DeltaDto>>(dto);
                // _logger.LogDebug("TraceId '{TraceId}': Received config deltas: {Deltas}", traceId, JsonConvert.SerializeObject(configDeltas));
                result = _crdtConfigs.Merge(configDeltas);
                  
                // TODO: maybe implement callback in ObservedRemoveMap instead 
                if(result is DeltaMergeResult.StateUpdated) CreateNewCrdtsIfAny();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return result;
    }

    private void EnsureAllReadReplicasMarkedLocally()
    {
        foreach (var replicaId in _crdtInfos.Keys)
        {
            _crdtInfos.TryGet(replicaId, info => info.ReadReplicas, out var readReplicas);
            Debug.Assert(readReplicas is not null);
            _crdts.TryGetValue(replicaId.InstanceId, out var crdt);
            Debug.Assert(crdt is not null);
            
            if (readReplicas.Contains(_thisNode.Id))
            {
                // _logger.LogDebug("Marking local replica {ReplicaId} as read replica", replicaId);
                crdt.MarkLocalShardAsReadReplica(replicaId.ShardId);
            }

            // Emergency measure. Read replicas are empty only if all nodes containing them went down in quick succession
            // This means data might have been lost. Mark first write replica as read replicas to keep things operational
            if (readReplicas.Count == 0)
            {
                if (!_desiredDistribution.TryGetValue(replicaId, out var nodeInfos))
                {
                    throw new AssumptionsViolatedException(
                        "Replica should not exist without being in the desired distribution");
                }
                Debug.Assert(!nodeInfos.IsDefaultOrEmpty, "desired distribution of a replica can not be empty");
                _crdtInfos.TryUpsertNodeAsHolderOfReadReplica(_thisNode.Id, nodeInfos[0].Id, replicaId, out _);
            }
        }
    }

    private void CreateNewCrdtsIfAny()
    {
        foreach (var instanceId in _crdtConfigs.Keys)
        {
            if (_crdts.ContainsKey(instanceId)) continue;

            _crdtConfigs.TryGet(instanceId, config => config.AssemblyQualifiedName, out var typeName);
            Debug.Assert(typeName is not null);

            _crdts[instanceId] = _crdtFactory.Create(typeName, instanceId, this);
        }
    }

}