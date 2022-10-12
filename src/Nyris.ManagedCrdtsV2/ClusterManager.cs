using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Crdt;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2;

public sealed class ClusterManager : INodeFailureObserver, IClusterMetadataManager, IReplicaDistributor, IManagedCrdtProvider
{
    private readonly ILogger<ClusterManager> _logger;
    private readonly ISerializer _serializer;
    private readonly IMetadataPropagationService _propagationService;
    private readonly IManagedCrdtFactory _crdtFactory;
    private readonly IDistributionStrategy _distributionStrategy;
    
    private readonly NodeInfo _thisNode;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private ImmutableDictionary<GlobalShardId, ImmutableArray<NodeInfo>> _desiredDistribution = ImmutableDictionary<GlobalShardId, ImmutableArray<NodeInfo>>.Empty;

    private readonly NodeInfoSet _nodeSet = new();
    private readonly CrdtInfos _crdtInfos = new();
    private readonly CrdtConfigs _crdtConfigs = new();
    private readonly ConcurrentDictionary<InstanceId, ManagedCrdt> _crdts = new();

    public ClusterManager(NodeInfo thisNode,
        ILogger<ClusterManager> logger,
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

    public async IAsyncEnumerable<(MetadataDto, ReadOnlyMemory<byte>)> AddNewNodeAsync(NodeInfo nodeInfo,
        OperationContext context)
    {
        // we don't want to accidentally propagate the update to a newly added node
        var nodesBeforeAddition = _nodeSet.Values.ToImmutableArray();
        var deltas = await AddNodeInfoAsync(nodeInfo, context.CancellationToken);
        await PropagateNodeDeltasAsync(deltas, nodesBeforeAddition, context);
        await DistributeShardsAsync(context.CancellationToken);

        yield return (MetadataDto.NodeSetFull, _serializer.Serialize(_nodeSet.ToDto()));
        foreach (var delta in _crdtConfigs.EnumerateDeltaDtos().Chunk(100))
        {
            yield return (MetadataDto.CrdtConfigs, _serializer.Serialize(delta));
        }
        foreach (var delta in _crdtInfos.EnumerateDeltaDtos().Chunk(100))
        {
            yield return (MetadataDto.CrdtInfos, _serializer.Serialize(delta));
        }
    }

    public async Task MergeAsync(MetadataDto kind, ReadOnlyMemory<byte> dto, OperationContext context)
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

    public ImmutableDictionary<MetadataDto, ReadOnlyMemory<byte>> GetCausalTimestamps(
        CancellationToken cancellationToken)
    {
        var builder = ImmutableDictionary.CreateBuilder<MetadataDto, ReadOnlyMemory<byte>>();

        var serialized = _serializer.Serialize(_nodeSet.GetLastKnownTimestamp());
        builder.Add(MetadataDto.NodeSet, serialized);
        
        serialized = _serializer.Serialize(_crdtConfigs.GetLastKnownTimestamp());
        builder.Add(MetadataDto.CrdtConfigs, serialized);
        
        serialized = _serializer.Serialize(_crdtInfos.GetLastKnownTimestamp());
        builder.Add(MetadataDto.CrdtInfos, serialized);

        return builder.ToImmutable();
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> EnumerateDeltasAsync(MetadataDto kind, 
        ReadOnlyMemory<byte> timestamp, 
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case MetadataDto.NodeSetFull:
            case MetadataDto.NodeSet:
                var nodesTimestamp = _serializer.Deserialize<NodeInfoSet.CausalTimestamp>(timestamp);
                foreach (var deltaDto in _nodeSet.EnumerateDeltaDtos(nodesTimestamp).Chunk(100))
                {
                    yield return _serializer.Serialize(deltaDto);
                }
                break;
            case MetadataDto.CrdtInfos:
                var infosTimestamp = _serializer.Deserialize<CrdtInfos.CausalTimestamp>(timestamp);
                foreach (var deltaDto in _crdtInfos.EnumerateDeltaDtos(infosTimestamp).Chunk(100))
                {
                    yield return _serializer.Serialize(deltaDto);
                }
                break;
            case MetadataDto.CrdtConfigs:
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

    public ImmutableArray<NodeInfo> GetNodesWithWriteReplicas(InstanceId instanceId, ShardId shardId) 
        => _desiredDistribution.TryGetValue(instanceId.With(shardId), out var nodes) 
            ? nodes 
            : ImmutableArray<NodeInfo>.Empty;

    public IReadOnlyCollection<NodeInfo> GetNodesWithReadReplicas(InstanceId instanceId, ShardId shardId)
    {
        throw new NotImplementedException();
    }

    public async Task NodeFailureObservedAsync(NodeId nodeId, CancellationToken cancellationToken)
    {
        if (_thisNode.Id == nodeId)
        {
            _logger.LogWarning("This should not be reachable - \"detected\" remote failure in itself, no-op");
        }

        var traceId = $"{_thisNode.Id}-observed-{nodeId}-fail";
        var context = new OperationContext(_thisNode.Id, -1, traceId, cancellationToken);
        ImmutableArray<NodeInfoSet.DeltaDto> deltas; 
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var nodeInfo = _nodeSet.Values.FirstOrDefault(ni => ni.Id == nodeId);
            if (nodeInfo is null) return;

            _logger.LogInformation("TraceId '{TraceId}': Failure in node '{NodeId}' detected, removing from NodeSet", 
                traceId, nodeId);
            deltas = _nodeSet.Remove(nodeInfo);
        }
        finally
        {
            _semaphore.Release();
        }
        
        await PropagateNodeDeltasAsync(deltas, _nodeSet.Values.ToImmutableArray(), context);
        await DistributeShardsAsync(cancellationToken);
    }

    public bool TryGet<TCrdt>(InstanceId instanceId, [NotNullWhen(true)] out TCrdt? crdt) 
        where TCrdt : ManagedCrdt
    {
        var result = _crdts.TryGetValue(instanceId, out var managedCrdt);
        crdt = managedCrdt as TCrdt;
        return result && crdt is not null;
    }

    public async Task<TCrdt> CreateAsync<TCrdt>(InstanceId instanceId, CancellationToken cancellationToken)
        where TCrdt : ManagedCrdt
    {
        var traceId = $"{instanceId.AsReadOnlySpan}-creation";
        var context = new OperationContext(_thisNode.Id, 1, traceId, cancellationToken);
        var crdt = _crdtFactory.Create<TCrdt>(instanceId, this);
        _crdts.TryAdd(instanceId, crdt);

        var shardIds = new[] { instanceId.With(default) };
        await Task.WhenAll(
            AddConfigAndPropagateAsync<TCrdt>(instanceId, context),
            AddInfosAndPropagateAsync(shardIds, context));
        await DistributeShardsAsync(cancellationToken); // intentionally awaited after previous two

        return crdt;
    }

    private DeltaMergeResult MergeInternal(MetadataDto kind, ReadOnlyMemory<byte> dto, string traceId)
    {
        var result = DeltaMergeResult.StateNotChanged;
        switch (kind)
        {
            case MetadataDto.NodeSet:
                var nodeDeltas = _serializer.Deserialize<ImmutableArray<NodeInfoSet.DeltaDto>>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received nodeSet deltas: {Deltas}", traceId, JsonConvert.SerializeObject(nodeDeltas));
                result = _nodeSet.Merge(nodeDeltas);
                break;
            case MetadataDto.NodeSetFull:
                // merging a full dto for a NodeSet is a special case used for Discovery, which should not trigger propagation
                var nodeDto = _serializer.Deserialize<NodeInfoSet.Dto>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received nodeSet dto: {Dto}", traceId, JsonConvert.SerializeObject(nodeDto));
                _nodeSet.Merge(nodeDto);
                break;
            case MetadataDto.CrdtInfos:
                var infoDeltas = _serializer.Deserialize<CrdtInfos.DeltaDto[]>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received info deltas: {Deltas}", traceId, JsonConvert.SerializeObject(infoDeltas));
                result = _crdtInfos.Merge(infoDeltas);
                break;
            case MetadataDto.CrdtConfigs:
                var configDeltas = _serializer.Deserialize<CrdtConfigs.DeltaDto[]>(dto);
                _logger.LogDebug("TraceId '{TraceId}': Received config deltas: {Deltas}", traceId, JsonConvert.SerializeObject(configDeltas));
                result = _crdtConfigs.Merge(configDeltas);
                CreateNewCrdtsIfAny();
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
        var shardInfosBuilder = ImmutableArray.CreateBuilder<ShardInfo>(_crdtInfos.Count);
        ImmutableArray<NodeInfo> nodeInfos;
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            nodeInfos = _nodeSet.Values.ToImmutableArray();
            foreach (var globalShardId in _crdtInfos.Keys)
            {
                _crdtConfigs.TryGet(globalShardId.InstanceId, config => config.RequestedReplicasCount,
                    out var numberOfDesiredReplicas);
                _crdtInfos.TryGet(globalShardId, info => info.StorageSize, out var size);
                shardInfosBuilder.Add(new ShardInfo(globalShardId, size, numberOfDesiredReplicas));
            }
        }
        finally
        {
            _semaphore.Release();
        }

        _desiredDistribution = _distributionStrategy.Distribute(shardInfosBuilder.MoveToImmutable(), nodeInfos);
    }
    
    private async Task<ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, NodeInfo>.DeltaDto>> AddNodeInfoAsync(NodeInfo nodeInfo, CancellationToken cancellationToken)
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
                RequestedReplicasCount = 2
            });
        }
        finally
        {
            _semaphore.Release();
        }
        var deltasBin = _serializer.Serialize(deltas);
        var nodes = _nodeSet.Values.ToImmutableArray();
        await _propagationService.PropagateAsync(MetadataDto.CrdtConfigs, deltasBin, nodes, context);
    }

    private async Task AddInfosAndPropagateAsync(GlobalShardId[] shardIds, OperationContext context)
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
        var data = _serializer.Serialize(infosDeltas.SelectMany(deltas => deltas));
        var nodes = _nodeSet.Values.ToImmutableArray();
        await _propagationService.PropagateAsync(MetadataDto.CrdtInfos, data, nodes, context);
    }

    private Task PropagateNodeDeltasAsync(ImmutableArray<NodeInfoSet.DeltaDto> deltas, ImmutableArray<NodeInfo> nodes, OperationContext context) 
        => _propagationService.PropagateAsync(MetadataDto.NodeSet, _serializer.Serialize(deltas), nodes, context);
}