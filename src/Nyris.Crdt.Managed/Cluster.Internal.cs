using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.ManagedCrdts.Factory;
using Nyris.Crdt.Managed.Metadata;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Managed.Strategies.Distribution;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.Crdt.Managed;

internal sealed partial class Cluster
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

    internal ICollection<InstanceId> InstanceIds => _crdts.Keys;

    internal async Task<ImmutableArray<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken)
        => _nodeSet.Values.ToImmutableArray();

    /// <summary>
    /// Make sure that:
    /// If this node holds a read replica of a provided (instanceId, shardId), then each node from syncedNodes
    /// also are in the list of nodes holding a given read replica.
    ///
    /// In other words - if we have successfully run relocation on read replica,
    /// all targets of that relocation should also be read replicas
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="shardId"></param>
    /// <param name="syncTargets"></param>
    /// <param name="context"></param>
    /// <exception cref="AssumptionsViolatedException"></exception>
    internal async Task ReportSyncSuccessfulAsync(InstanceId instanceId, ShardId shardId, ImmutableArray<NodeInfo> syncTargets, OperationContext context)
    {
        var deltas = new List<ImmutableArray<CrdtInfos.DeltaDto>>(syncTargets.Length);
        var replicaId = instanceId.With(shardId);

        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            if(!_crdtInfos.TryGet(replicaId, info => info.ReadReplicas, out var readReplicas))
            {
                throw new AssumptionsViolatedException($"Should not be possible - it appears that replica {replicaId} has no CrdtInfo");
            }
            if (!_desiredDistribution.TryGetValue(replicaId, out var writeReplicas))
            {
                throw new AssumptionsViolatedException($"Should not be possible - it appears that replica {replicaId} is not within desired distribution");
            }

            Debug.Assert(readReplicas is not null && readReplicas.Count != 0 && writeReplicas.Length != 0);
            switch (readReplicas.Contains(_thisNode.Id), writeReplicas.Contains(_thisNode))
            {
                // Stable picture - replica is both read and write.
                // Make sure that target nodes are also read replicas now (no op if they are already read replicas)
                case (true, true):
                    EnsureTargetNodesAreReadReplicas(syncTargets, replicaId, deltas);
                    break;
                // this node is meant to hold this replica, but it was not relocated here yet. We can't
                // mark this replica as "read" - another read replica must sync 'into' this node. So do nothing
                case (false, true):
                    break;
                // this node was meant to hold this replica in the past, but not anymore. We just relocated
                // this replica, so we
                // (1) mark target node as holding a read replica,
                // (2) remove this node from read replicas and
                // (3) drop local shard
                case (true, false):
                    EnsureTargetNodesAreReadReplicas(syncTargets, replicaId, deltas);
                    await RemoveThisNodeFromReadReplicasAndDropLocalAsync(replicaId, deltas);
                    break;
                // this node is neither read nor write. This might be due to updates that were ok to accept
                // locally without consulting with full data in read replica. In this case we simply drop
                // the local replica (it's ok to do as SynchronisationService calls this method inside a lock)
                case (false, false):
                    await DropLocalReplicaAsync(replicaId);
                    break;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        if (deltas.Count != 0)
        {
            LogDistributions();
            await PropagateInfosAsync(deltas.SelectMany(array => array), context);
        }
    }

    private async Task RemoveThisNodeFromReadReplicasAndDropLocalAsync(ReplicaId replicaId, List<ImmutableArray<CrdtInfos.DeltaDto>> deltas)
    {
        _crdtInfos.TryRemoveNodeAsHolderOfReadReplica(_thisNode.Id, _thisNode.Id, replicaId, out var removeDeltas);
        if (!removeDeltas.IsDefaultOrEmpty)
        {
            deltas.Add(removeDeltas);
        }

        await DropLocalReplicaAsync(replicaId);
    }

    private async Task DropLocalReplicaAsync(ReplicaId replicaId)
    {
        if (!_crdts.TryGetValue(replicaId.InstanceId, out var crdt))
        {
            throw new AssumptionsViolatedException(
                $"Should not be possible - it appears that replica {replicaId} has no ManagedCrdt stored locally");
        }

        await crdt.DropShardAsync(replicaId.ShardId);
    }

    private void EnsureTargetNodesAreReadReplicas(ImmutableArray<NodeInfo> syncTargets,
        ReplicaId replicaId,
        List<ImmutableArray<CrdtInfos.DeltaDto>> deltas)
    {
        foreach (var syncTarget in syncTargets)
        {
            _crdtInfos.TryUpsertNodeAsHolderOfReadReplica(_thisNode.Id, syncTarget.Id, replicaId, out var upsertDeltas);
            if (!upsertDeltas.IsDefaultOrEmpty)
            {
                deltas.Add(upsertDeltas);
            }
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
        foreach (var instanceId in _crdts.Keys.OrderBy(k => k))
        {
            TryGet<ManagedCrdt>(instanceId, out var crdt);
            var sizes = crdt!.GetShardSizes();

            foreach (var (shardId, size) in sizes.OrderBy(pair => pair.Key))
            {
                builder.Append('(').Append(instanceId).Append(", ").Append(shardId).Append("): ").Append(size).Append(", ");
            }
        }
        _logger.LogDebug("Actual local shards sizes: {Sizes}", builder.ToString());
    }

    private async Task DistributeShardsAsync(CancellationToken cancellationToken)
    {
        ImmutableArray<ReplicaInfo>.Builder replicaInfosBuilder;
        ICollection<NodeInfo> nodes;

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


    private Task PropagateInfosAsync(IEnumerable<CrdtInfos.DeltaDto> deltas, OperationContext context)
        => PropagateInfosAsync(_serializer.Serialize(deltas), context);

    private Task PropagateInfosAsync(ImmutableArray<CrdtInfos.DeltaDto> deltas, OperationContext context)
        => PropagateInfosAsync(_serializer.Serialize(deltas), context);

    private Task PropagateInfosAsync(ReadOnlyMemory<byte> deltas, OperationContext context) =>
        _propagationService.PropagateAsync(MetadataKind.CrdtInfos,
            deltas,
            _nodeSet.Values.ToImmutableArray(),
            context);

    private Task PropagateNodeDeltasAsync(ImmutableArray<NodeInfoSet.DeltaDto> deltas, ImmutableArray<NodeInfo> nodes, OperationContext context)
        => _propagationService.PropagateAsync(MetadataKind.NodeSet, _serializer.Serialize(deltas), nodes, context);
}
