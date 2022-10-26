using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.ManagedCrdts.Factory;
using Nyris.Crdt.Managed.Metadata;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Managed.Strategies.Distribution;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed;

internal sealed class Cluster : ICluster, INodeFailureObserver, IClusterMetadataManager, IReplicaDistributor, IManagedCrdtProvider
{
    private readonly ILogger<Cluster> _logger;
    private readonly ISerializer _serializer;
    private readonly IMetadataPropagationService _propagationService;
    private readonly IManagedCrdtFactory _crdtFactory;
    private readonly IDistributionStrategy _distributionStrategy;
    
    private readonly NodeInfo _thisNode;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private ImmutableDictionary<ReplicaId, ImmutableArray<NodeInfo>> _desiredDistribution = ImmutableDictionary<ReplicaId, ImmutableArray<NodeInfo>>.Empty;

    private readonly NodeInfoSet _nodeSet = new();
    private readonly CrdtInfos _crdtInfos = new();
    private readonly CrdtConfigs _crdtConfigs = new();
    private readonly ConcurrentDictionary<InstanceId, ManagedCrdt> _crdts = new();

    public Cluster(NodeInfo thisNode,
        ILogger<Cluster> logger,
        ISerializer serializer,
        IMetadataPropagationService propagationService,
        IManagedCrdtFactory crdtFactory,
        IDistributionStrategy distributionStrategy)
    {
        _thisNode = thisNode;
        _logger = logger;
        _serializer = serializer;
        _propagationService = propagationService;
        _crdtFactory = crdtFactory;
        _distributionStrategy = distributionStrategy;
        _nodeSet.Add(thisNode, thisNode.Id);
    }

    public ICollection<InstanceId> InstanceIds => _crdts.Keys;

    public async IAsyncEnumerable<(MetadataKind, ReadOnlyMemory<byte>)> AddNewNodeAsync(NodeInfo nodeInfo,
        OperationContext context)
    {
        // we don't want to accidentally propagate the update to a newly added node
        var nodesBeforeAddition = _nodeSet.Values.ToImmutableArray();
        var deltas = await AddNodeInfoAsync(nodeInfo, context.CancellationToken);
        await PropagateNodeDeltasAsync(deltas, nodesBeforeAddition, context);
        await DistributeShardsAsync(context.CancellationToken);

        yield return (MetadataKind.NodeSetFull, _serializer.Serialize(_nodeSet.ToDto()));
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
            _logger.LogDebug("TraceId '{TraceId}': After merging {Kind} delta state was updated, propagating further", 
                context.TraceId, kind.ToString("G"));
            await _propagationService.PropagateAsync(kind, dto, nodeSetBeforeMerge, context);
            await DistributeShardsAsync(context.CancellationToken);
        }
    }

    public async Task<ImmutableArray<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken) 
        => _nodeSet.Values.ToImmutableArray();

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
            case MetadataKind.NodeSetFull:
            case MetadataKind.NodeSet:
                var nodesTimestamp = _serializer.Deserialize<ObservedRemoveDtos<NodeId, NodeInfo>.CausalTimestamp>(timestamp);
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

    public async Task ReportSyncSuccessfulAsync(InstanceId instanceId, ShardId shardId,OperationContext context)
    {
        var deltas = ImmutableArray<CrdtInfos.DeltaDto>.Empty;
        
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            var replicaId = instanceId.With(shardId);
            if (!_desiredDistribution.TryGetValue(replicaId, out var writeReplicas)) return;

            var shouldThisNodeHaveReplica = writeReplicas.Contains(_thisNode);
            
            if (shouldThisNodeHaveReplica // using short-circuit AND - try upsetting only if shouldThisNodeHaveReplica is TRUE
                && !_crdtInfos.TryUpsertNodeAsHolderOfReadReplica(_thisNode.Id, replicaId, out deltas))
            {
                _logger.LogWarning("Should not be possible - it appears that replica {Replica} has no CrdtInfo", replicaId.ToString());
            }

            if (!shouldThisNodeHaveReplica // using short-circuit AND - try removing only if shouldThisNodeHaveReplica is FALSE
                && !_crdtInfos.TryRemoveNodeAsHolderOfReadReplica(_thisNode.Id, replicaId, out deltas))
            {
                _logger.LogWarning("Should not be possible - it appears that replica {Replica} has no CrdtInfo", replicaId.ToString());
            }
        }
        finally
        {
            _semaphore.Release();
        }
        
        LogDistributions();

        if (!deltas.IsEmpty)
        {
            await PropagateInfosAsync(deltas, context);
        }
    }

    private void LogDistributions()
    {
        _logger.LogDebug("Write replicas: {Dist}", _desiredDistribution.OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key.ToString()}: " +
                            $"[{string.Join(",", pair.Value.Select(ni => ni.Id.ToString()))}]"));

        var readDist = new Dictionary<ReplicaId, IEnumerable<NodeId>>();
        foreach (var id in _crdtInfos.Keys)
        {
            _crdtInfos.TryGet(id, info => info.ReadReplicas, out var replicas);
            readDist.Add(id, replicas ?? Enumerable.Empty<NodeId>());
        }

        _logger.LogDebug("Read replicas: {Dist}", readDist.OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key.ToString()}: " +
                            $"[{string.Join(",", pair.Value.Select(i => i.ToString()))}]"));

        var builder = new StringBuilder("Actual local shard sizes: ");
        foreach (var replicaId in _crdtInfos.Keys.OrderBy(k => k))
        {
            TryGet<ManagedCrdt>(replicaId.InstanceId, out var crdt);
            var size = crdt!.GetShardSize(replicaId.ShardId);
            builder.Append(replicaId.ToString()).Append(' ').Append(size).Append(", ");
        }
        _logger.LogDebug("Actual local shards sizes: {Sizes}", builder.ToString());
    }

    public ImmutableArray<NodeInfo> GetNodesWithWriteReplicas(InstanceId instanceId, ShardId shardId) 
        => _desiredDistribution.TryGetValue(instanceId.With(shardId), out var nodes) 
            ? nodes 
            : ImmutableArray<NodeInfo>.Empty;

    public ImmutableArray<NodeInfo> GetNodesWithReadReplicas(InstanceId instanceId, ShardId shardId)
    {
        if (!_crdtInfos.TryGet(instanceId.With(shardId), crdtInfo => crdtInfo.ReadReplicas, out var replicas)
            || replicas is null 
            || replicas.Count == 0)
        {
            return ImmutableArray<NodeInfo>.Empty;
        }

        // TODO: refactor this abomination - cache read replica upon change in CrdtInfos
        var builder = ImmutableArray.CreateBuilder<NodeInfo>(replicas.Count);
        var nodes = _nodeSet.Values.ToDictionary(ni => ni.Id, ni => ni);
        foreach (var nodeId in replicas.OrderBy(i => i))
        {
            if (nodes.TryGetValue(nodeId, out var info))
            {
                builder.Add(info);    
            }
        }
        return builder.MoveToImmutable();
    }

    public async Task NodeFailureObservedAsync(NodeId nodeId, CancellationToken cancellationToken)
    {
        if (_thisNode.Id == nodeId)
        {
            _logger.LogCritical("This should not be reachable - \"detected\" remote failure in itself, no-op");
            return;
        }

        var traceId = $"{_thisNode.Id}-observed-{nodeId}-fail";
        var context = new OperationContext(_thisNode.Id, -1, traceId, cancellationToken);
        ImmutableArray<ObservedRemoveDtos<NodeId, NodeInfo>.DeltaDto> nodesDeltas;
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
                if (_crdtInfos.TryRemoveNodeAsHolderOfReadReplica(nodeId, replicaId, out var infoDeltas))
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

    public bool TryGet<TCrdt>(InstanceId instanceId, [NotNullWhen(true)] out TCrdt? crdt) 
        where TCrdt : ManagedCrdt
    {
        var result = _crdts.TryGetValue(instanceId, out var managedCrdt);
        crdt = managedCrdt as TCrdt;
        return result && crdt is not null;
    }

    public bool TryGet(InstanceId instanceId, [NotNullWhen(true)] out IManagedCrdt? crdt)
    {
        var result = _crdts.TryGetValue(instanceId, out var value);
        crdt = value;
        return result;
    }

    public async Task<TCrdt> CreateAsync<TCrdt>(InstanceId instanceId, CancellationToken cancellationToken)
        where TCrdt : ManagedCrdt
    {
        var traceId = $"{instanceId.ToString()}-creation";
        var context = new OperationContext(_thisNode.Id, -1, traceId, cancellationToken);
        var crdt = _crdtFactory.Create<TCrdt>(instanceId, this);
        _crdts.TryAdd(instanceId, crdt);

        var shardIds = new[]
        { 
            instanceId.With(ShardId.FromUint(0)),
            instanceId.With(ShardId.FromUint(1)), 
            instanceId.With(ShardId.FromUint(2))
        };
        await Task.WhenAll(
            AddConfigAndPropagateAsync<TCrdt>(instanceId, context),
            AddInfosAndPropagateAsync(shardIds, context));
        await DistributeShardsAsync(cancellationToken); // intentionally awaited after previous two

        return crdt;
    }

    private DeltaMergeResult MergeInternal(MetadataKind kind, ReadOnlyMemory<byte> dto, string traceId)
    {
        var result = DeltaMergeResult.StateNotChanged;
        switch (kind)
        {
            case MetadataKind.NodeSet:
                var nodeDeltas = _serializer.Deserialize<ImmutableArray<ObservedRemoveDtos<NodeId, NodeInfo>.DeltaDto>>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received nodeSet deltas: {Deltas}", traceId, JsonConvert.SerializeObject(nodeDeltas));
                result = _nodeSet.Merge(nodeDeltas);
                break;
            case MetadataKind.NodeSetFull:
                // merging a full dto for a NodeSet is a special case used for Discovery, which should not trigger propagation
                var nodeDto = _serializer.Deserialize<ObservedRemoveDtos<NodeId, NodeInfo>.Dto>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received nodeSet dto: {Dto}", traceId, JsonConvert.SerializeObject(nodeDto));
                _nodeSet.Merge(nodeDto);
                break;
            case MetadataKind.CrdtInfos:
                var infoDeltas = _serializer.Deserialize<CrdtInfos.DeltaDto[]>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received info deltas: {Deltas}", traceId, JsonConvert.SerializeObject(infoDeltas));
                result = _crdtInfos.Merge(infoDeltas);
                break;
            case MetadataKind.CrdtConfigs:
                var configDeltas = _serializer.Deserialize<CrdtConfigs.DeltaDto[]>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received config deltas: {Deltas}", traceId, JsonConvert.SerializeObject(configDeltas));
                result = _crdtConfigs.Merge(configDeltas);
                CreateNewCrdtsIfAny();  // TODO: use data change callbacks once implemented
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return result;
    }

    private void CreateNewCrdtsIfAny()
    {
        foreach (var instanceId in _crdtConfigs.Keys)
        {
            if (_crdts.ContainsKey(instanceId)) continue;

            _crdtConfigs.TryGet(instanceId, config => config.FullTypeName, out var typeName);
            Debug.Assert(typeName is not null);

            _crdts[instanceId] = _crdtFactory.Create(typeName, instanceId, this);
        }
    }

    private async Task DistributeShardsAsync(CancellationToken cancellationToken)
    {
        ImmutableArray<ReplicaInfo>.Builder replicaInfosBuilder;
        HashSet<NodeInfo> nodes;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            replicaInfosBuilder = ImmutableArray.CreateBuilder<ReplicaInfo>(_crdtInfos.Count);

            nodes = _nodeSet.Values;
            foreach (var replicaId in _crdtInfos.Keys)
            {
                _crdtConfigs.TryGet(replicaId.InstanceId, config => config.RequestedReplicasCount,
                    out var requestedReplicaCount);
                _crdtInfos.TryGet(replicaId, info => info.StorageSize, out var size);
                replicaInfosBuilder.Add(new ReplicaInfo(replicaId, size, requestedReplicaCount));
            }
        }
        finally
        {
            _semaphore.Release();
        }

        
        replicaInfosBuilder.Sort();

        var nodeInfosBuilder = ImmutableArray.CreateBuilder<NodeInfo>(nodes.Count);
        nodeInfosBuilder.AddRange(nodes);
        nodeInfosBuilder.Sort();
        _desiredDistribution = _distributionStrategy.Distribute(replicaInfosBuilder.MoveToImmutable(), nodeInfosBuilder.MoveToImmutable());
    }
    
    private async Task<ImmutableArray<ObservedRemoveDtos<NodeId, NodeInfo>.DeltaDto>> AddNodeInfoAsync(NodeInfo nodeInfo, CancellationToken cancellationToken)
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

    private async Task AddConfigAndPropagateAsync<TCrdt>(InstanceId instanceId, OperationContext context)
    {
        ImmutableArray<CrdtConfigs.DeltaDto> deltas;
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            deltas = _crdtConfigs.AddOrMerge(_thisNode.Id, instanceId, new CrdtConfig
            {
                FullTypeName = typeof(TCrdt).FullName ?? throw new AssumptionsViolatedException($"Type {typeof(TCrdt)} expected to have a full name"),
                RequestedReplicasCount = 2,
                NumberOfShards = 3  // TODO: allow user to set these
            });
        }
        finally
        {
            _semaphore.Release();
        }
        var deltasBin = _serializer.Serialize(deltas);
        var nodes = _nodeSet.Values.ToImmutableArray();
        await _propagationService.PropagateAsync(MetadataKind.CrdtConfigs, deltasBin, nodes, context);
    }

    private async Task AddInfosAndPropagateAsync(ReplicaId[] shardIds, OperationContext context)
    {
        var infosDeltas = new ImmutableArray<CrdtInfos.DeltaDto>[shardIds.Length];
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            for (var i = 0; i < shardIds.Length; i++)
            {
                infosDeltas[i] = _crdtInfos.AddOrMerge(_thisNode.Id, shardIds[i], new CrdtInfo());
            }
        }
        finally
        {
            _semaphore.Release();
        }
        await PropagateInfosAsync(infosDeltas.SelectMany(deltas => deltas), context);
    }

    private Task PropagateInfosAsync(IEnumerable<CrdtInfos.DeltaDto> deltas, OperationContext context)  
        => PropagateInfosAsync(_serializer.Serialize(deltas), context);
    
    private Task PropagateInfosAsync(ImmutableArray<CrdtInfos.DeltaDto> deltas, OperationContext context) 
        => PropagateInfosAsync(_serializer.Serialize(deltas), context);

    private Task PropagateInfosAsync(ReadOnlyMemory<byte> deltas, OperationContext context) =>
        _propagationService.PropagateAsync(MetadataKind.CrdtInfos, 
            deltas, 
            _nodeSet.Values.ToImmutableArray(),
            context);
    
    private Task PropagateNodeDeltasAsync(ImmutableArray<ObservedRemoveDtos<NodeId, NodeInfo>.DeltaDto> deltas, ImmutableArray<NodeInfo> nodes, OperationContext context) 
        => _propagationService.PropagateAsync(MetadataKind.NodeSet, _serializer.Serialize(deltas), nodes, context);
}