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

    public async Task<ReadOnlyMemory<byte>> AddNewNodeAsync(NodeInfo nodeInfo, CancellationToken cancellationToken = default)
    {
        // we don't want to accidentally propagate the update to a newly added node
        var nodesBeforeAddition = _nodeSet.Values;
        var deltas = await AddNodeInfoAsync(nodeInfo, cancellationToken);
        await PropagateNodeDeltasAsync(deltas, nodesBeforeAddition, cancellationToken);
        await DistributeShardsAsync(cancellationToken);
        return _serializer.Serialize(_nodeSet.ToDto());
    }

    public async Task MergeAsync(MetadataDto kind, ReadOnlyMemory<byte> dto, CancellationToken cancellationToken = default)
    {
        var nodeSetBeforeMerge = _nodeSet.Values;
        DeltaMergeResult result;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            result = MergeInternal(kind, dto);
        }
        finally
        {
            _semaphore.Release();
        }

        if (result == DeltaMergeResult.StateUpdated)
        {
            await _propagationService.PropagateAsync(kind, dto, nodeSetBeforeMerge, cancellationToken);
            await DistributeShardsAsync(cancellationToken);
        }
    }
    
    public ImmutableArray<NodeInfo> GetDesiredNodesWithReplicas(InstanceId instanceId, ShardId shardId) 
        => _desiredDistribution.TryGetValue(instanceId.With(shardId), out var nodes) 
            ? nodes 
            : ImmutableArray<NodeInfo>.Empty;

    public IReadOnlyCollection<NodeInfo> GetCurrentNodesWithReplicas(InstanceId instanceId, ShardId shardId)
    {
        throw new NotImplementedException();
    }

    public async Task NodeFailureObservedAsync(NodeId nodeId, CancellationToken cancellationToken)
    {
        if(_thisNode.Id == nodeId) _logger.LogInformation("This node \"detected\" remote failure in itself, no-op");

        ImmutableArray<NodeInfoSet.DeltaDto> deltas; 
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var nodeInfo = _nodeSet.Values.FirstOrDefault(ni => ni.Id == nodeId);
            if (nodeInfo is null) return;

            _logger.LogInformation("Failure in node '{NodeId}' detected, removing from NodeSet", nodeId);
            deltas = _nodeSet.Remove(nodeInfo);
        }
        finally
        {
            _semaphore.Release();
        }
        
        await PropagateNodeDeltasAsync(deltas, _nodeSet.Values, cancellationToken);
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
        var crdt = _crdtFactory.Create<TCrdt>(instanceId, this);
        _crdts.TryAdd(instanceId, crdt);

        var shardIds = new[] { instanceId.With(default) };
        await Task.WhenAll(
            AddConfigAndPropagateAsync<TCrdt>(instanceId, cancellationToken),
            AddInfosAndPropagateAsync(shardIds, cancellationToken));
        await DistributeShardsAsync(cancellationToken); // intentionally awaited after previous two

        return crdt;
    }

    private DeltaMergeResult MergeInternal(MetadataDto kind, ReadOnlyMemory<byte> dto)
    {
        var result = DeltaMergeResult.StateNotChanged;
        switch (kind)
        {
            case MetadataDto.NodeSet:
                var nodeDeltas = _serializer.Deserialize<ImmutableArray<NodeInfoSet.DeltaDto>>(dto);
                _logger.LogDebug("Received nodeSet deltas: {Deltas}", JsonConvert.SerializeObject(nodeDeltas));
                result = _nodeSet.Merge(nodeDeltas);
                break;
            case MetadataDto.NodeSetFull:
                // merging a full dto for a NodeSet is a special case used for Discovery, which should not trigger propagation
                var nodeDto = _serializer.Deserialize<NodeInfoSet.Dto>(dto);
                _logger.LogDebug("Received nodeSet dto: {Dto}", JsonConvert.SerializeObject(nodeDto));
                _nodeSet.Merge(nodeDto);
                break;
            case MetadataDto.CrdtInfos:
                var infoDeltas = _serializer.Deserialize<CrdtInfos.DeltaDto[]>(dto);
                _logger.LogDebug("Received info deltas: {Deltas}", JsonConvert.SerializeObject(infoDeltas));
                result = _crdtInfos.Merge(infoDeltas);
                break;
            case MetadataDto.CrdtConfigs:
                var configDeltas = _serializer.Deserialize<CrdtConfigs.DeltaDto[]>(dto);
                _logger.LogDebug("Received config deltas: {Deltas}", JsonConvert.SerializeObject(configDeltas));
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

    private async Task AddConfigAndPropagateAsync<TCrdt>(InstanceId instanceId, CancellationToken cancellationToken)
    {
        ImmutableArray<CrdtConfigs.DeltaDto> deltas;
        await _semaphore.WaitAsync(cancellationToken);
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
        await _propagationService.PropagateAsync(MetadataDto.CrdtConfigs, deltasBin, _nodeSet.Values, cancellationToken);
    }

    private async Task AddInfosAndPropagateAsync(GlobalShardId[] shardIds, CancellationToken cancellationToken)
    {
        var infosDeltas = new ImmutableArray<CrdtInfos.DeltaDto>[shardIds.Length];
        await _semaphore.WaitAsync(cancellationToken);
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
        await _propagationService.PropagateAsync(MetadataDto.CrdtInfos, data, _nodeSet.Values, cancellationToken);
    }

    private Task PropagateNodeDeltasAsync(ImmutableArray<NodeInfoSet.DeltaDto> deltas, IReadOnlyCollection<NodeInfo> nodes, CancellationToken cancellationToken) 
        => _propagationService.PropagateAsync(MetadataDto.NodeSet, _serializer.Serialize(deltas), nodes, cancellationToken);
}