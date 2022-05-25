using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nyris.Contracts.Exceptions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;
using Nyris.Extensions.Guids;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Metrics;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions;

/// <summary>
/// The registry is a key -> collection mapping, where each collection is replicated to a subset of other nodes.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TCollection"></typeparam>
/// <typeparam name="TCollectionDto"></typeparam>
/// <typeparam name="TCollectionOperationBase"></typeparam>
/// <typeparam name="TCollectionFactory"></typeparam>
/// <typeparam name="TCollectionOperationResponseBase"></typeparam>
/// <typeparam name="TCollectionValue"></typeparam>
/// <typeparam name="TCollectionKey"></typeparam>
public abstract class PartiallyReplicatedCRDTRegistry<TKey,
                                                      TCollection,
                                                      TCollectionKey,
                                                      TCollectionValue,
                                                      TCollectionDto,
                                                      TCollectionOperationBase,
                                                      TCollectionOperationResponseBase,
                                                      TCollectionFactory>
    : ManagedCRDT<PartiallyReplicatedCRDTRegistry<TKey,
          TCollection,
          TCollectionKey,
          TCollectionValue,
          TCollectionDto,
          TCollectionOperationBase,
          TCollectionOperationResponseBase,
          TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>,
      ICreateAndDeleteManagedCrdtsInside,
      IReactToOtherCrdtChange,
      INodesWithReplicaProvider,
      IDisposable
    where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
    where TCollection : ManagedCrdtRegistryBase<TCollectionKey, TCollectionValue, TCollectionDto>,
    IAcceptOperations<TCollectionOperationBase, TCollectionOperationResponseBase>
    where TCollectionOperationBase : Operation, ISelectShards
    where TCollectionOperationResponseBase : OperationResponse
    where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionDto>, new()
    where TCollectionKey : notnull
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Random Random = new();
    private readonly TCollectionFactory _factory;
    private readonly IResponseCombinator _responseCombinator;
    private readonly Task _refreshShardSizesTask;
    private readonly CancellationTokenSource _cts = new();

    private readonly ILogger? _logger;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private readonly NodeInfo _thisNode;
    private readonly IPartialReplicationStrategy _partialReplicationStrategy;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private ManagedCrdtContext? _context;
    private readonly IChannelManager? _channelManager;
    private readonly ICrdtMetricsRegistry? _metricsRegistry;

    private IDictionary<ShardId, IList<NodeInfo>> _desiredDistribution;

    private readonly ConcurrentDictionary<ShardId, TCollection> _collections;

    // CollectionInfo += sharding config
    /// <summary>
    /// Keys of all collections, regardless if they are stored locally or not
    /// </summary>
    private readonly HashableCrdtRegistry<NodeId,
        TKey,
        CollectionInfo,
        CollectionInfo.CollectionInfoDto,
        CollectionInfo.CollectionInfoFactory> _collectionInfos;

    /// <summary>
    /// Mapping: ShardId -> HashSet{NodeId}: shows which node contains which collections. Used for routing requests
    /// </summary>
    private readonly HashableCrdtRegistry<NodeId,
        ShardId,
        HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
        HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
        HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory> _currentState;

    /// <inheritdoc />
    protected PartiallyReplicatedCRDTRegistry(
        InstanceId instanceId,
        ILogger? logger = null,
        IPartialReplicationStrategy? partialReplicationStrategy = null,
        IResponseCombinator? responseCombinator = null,
        INodeInfoProvider? nodeInfoProvider = null,
        IAsyncQueueProvider? queueProvider = null,
        IChannelManager? channelManager = null,
        TCollectionFactory? factory = default,
        ICrdtMetricsRegistry? metricsRegistry = null
    ) : base(instanceId, queueProvider: queueProvider, logger: logger)
    {
        _logger = logger;
        _responseCombinator = responseCombinator ?? DefaultConfiguration.ResponseCombinator;
        _thisNode = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
        _partialReplicationStrategy = partialReplicationStrategy ?? DefaultConfiguration.PartialReplicationStrategy;
        _collectionInfos =
            new HashableCrdtRegistry<NodeId, TKey, CollectionInfo, CollectionInfo.CollectionInfoDto,
                CollectionInfo.CollectionInfoFactory>();
        _currentState = new();
        _collections = new();
        _desiredDistribution = new Dictionary<ShardId, IList<NodeInfo>>();
        _factory = factory ?? new TCollectionFactory();
        _channelManager = channelManager;
        _metricsRegistry = metricsRegistry;
        _refreshShardSizesTask = RefreshShardSizesAsync(_cts.Token);
    }

    /// <inheritdoc />
    public ManagedCrdtContext ManagedCrdtContext
    {
        get => _context ?? throw new ManagedCrdtContextSetupException(
                                                                      $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
        set => _context = value;
    }

    public async Task<Dictionary<ShardId, IDictionary<TCollectionKey, TCollectionValue>>> GetLocalShardsAsync()
    {
        var result = new Dictionary<ShardId, IDictionary<TCollectionKey, TCollectionValue>>(_collections.Count);
        foreach (var shardId in _collections.Keys.OrderBy(k => k))
        {
            var dict = new Dictionary<TCollectionKey, TCollectionValue>((int) _collections[shardId].Size);
            await foreach (var (key, item) in _collections[shardId].EnumerateItems())
            {
                dict[key] = item;
            }

            result[shardId] = dict;
        }

        return result;
    }

    public async Task<Dictionary<ShardId, ICollection<TCollectionDto>>> GetLocalShardDtosAsync()
    {
        var result = new Dictionary<ShardId, ICollection<TCollectionDto>>(_collections.Count);
        foreach (var shardId in _collections.Keys.OrderBy(k => k))
        {
            var list = new List<TCollectionDto>((int) _collections[shardId].StorageSize);
            await foreach (var dto in _collections[shardId].EnumerateDtoBatchesAsync())
            {
                list.Add(dto);
            }

            result[shardId] = list;
        }

        return result;
    }

    public async Task<bool> TryAddCollectionAsync(
        TKey key,
        CollectionConfig config,
        uint propagateToNodes = 0,
        string? traceId = null,
        CancellationToken cancellationToken = default
    )
    {
        traceId ??= ShortGuid.Encode(Guid.NewGuid());
        var result = false;

        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            var shardIds = Enumerable
                           .Range(0, config.ShardingConfig?.NumShards ?? 1)
                           .Select(_ => ShardId.GenerateNew());

            var collectionInfo = new CollectionInfo(name: config.Name,
                                                    shardIds: shardIds,
                                                    indexes: config.IndexNames);
            result = _collectionInfos.TryAdd(_thisNode.Id, key, collectionInfo);

            _metricsRegistry?.CollectCollectionSize((int?) collectionInfo.Size, TypeName);
        }
        finally
        {
            _semaphore.Release();
        }

        await RebalanceAsync(traceId: traceId, cancellationToken: cancellationToken);
        MaybeUpdateCurrentState();
        await StateChangedAsync(propagateToNodes, false, traceId, cancellationToken);
        // _logger?.LogDebug("TraceId: {TraceId}, changes to registry propagated, end of {FuncName}",
        //     traceId, nameof(TryAddCollectionAsync));
        return result;
    }

    public async Task<bool> TryRemoveCollectionAsync(
        TKey key,
        uint propagateToNodes = 0,
        string? traceId = null,
        CancellationToken cancellationToken = default
    )
    {
        traceId ??= ShortGuid.Encode(Guid.NewGuid());
        var result = false;
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            if (_collectionInfos.TryGetValue(key, out var info))
            {
                _collectionInfos.Remove(key);
                foreach (var shardId in info.Shards.Keys)
                {
                    _currentState.Remove(shardId);
                    if (_collections.TryRemove(shardId, out var collection))
                    {
                        ManagedCrdtContext.Remove<TCollection, TCollectionDto>(collection);
                    }
                }
            }

            result = true;
        }
        finally
        {
            _semaphore.Release();
        }

        await RebalanceAsync(traceId: traceId, cancellationToken: cancellationToken);
        await StateChangedAsync(propagateToNodes, false, traceId, cancellationToken);
        return result;
    }

    public bool CollectionExists(TKey key) => _collectionInfos.ContainsKey(key);

    public bool TryGetCollectionSize(TKey key, out ulong size, out ulong storageSize)
    {
        if (!_collectionInfos.TryGetValue(key, out var collectionInfo))
        {
            size = 0;
            storageSize = 0;
            return false;
        }

        size = collectionInfo.Size;
        storageSize = collectionInfo.StorageSize;
        return true;
    }

    public async Task<Response<TResponse>> ApplyAsync<TOperation, TResponse>(
        TKey key,
        TOperation operation,
        string? traceId = null,
        CancellationToken cancellationToken = default
    )
        where TOperation : TCollectionOperationBase
        where TResponse : TCollectionOperationResponseBase
    {
        traceId ??= ShortGuid.Encode(Guid.NewGuid());
        // _logger?.LogDebug("TraceId {TraceId}, registry received an operation for collection " +
        //                   "{CollectionKey} of type {OperationType}", traceId, key, typeof(TOperation));
        if (!_collectionInfos.TryGetValue(key, out var info))
        {
            return Response<TResponse>.Fail($"{key} was not found in the collectionInfos");
        }

        var routeToShards = operation.GetShards(info.Shards.Keys).ToList();

        switch (routeToShards.Count)
        {
            case 0:
                throw new NyrisException($"Operation {operation.GetType()} returned empty list of shards");
            case 1:
                return await ApplyToSingleShardAsync<TOperation, TResponse>(routeToShards[0],
                                                                            operation,
                                                                            traceId,
                                                                            cancellationToken);
        }

        var tasks = new Task<Response<TResponse>>[routeToShards.Count];
        for (var i = 0; i < routeToShards.Count; ++i)
        {
            tasks[i] = ApplyToSingleShardAsync<TOperation, TResponse>(routeToShards[i],
                                                                      operation,
                                                                      traceId,
                                                                      cancellationToken);
        }

        await Task.WhenAll(tasks);

        return _responseCombinator.Combine(tasks.Select(t => t.Result.Value!));
    }

    internal async Task<Response<TResponse>> ApplyToSingleShardAsync<TOperation, TResponse>(
        ShardId shardId,
        TOperation operation,
        string traceId,
        CancellationToken cancellationToken = default
    )
        where TOperation : TCollectionOperationBase
        where TResponse : TCollectionOperationResponseBase
    {
        if (!_currentState.TryGetValue(shardId, out var nodesWithShard))
        {
            return Response<TResponse>.Fail($"{shardId} was not found in the current state");
        }

        if (nodesWithShard.Contains(_thisNode.Id) && _collections.TryGetValue(shardId, out var collection))
        {
            // _logger?.LogDebug("TraceId {TraceId}, collection {CollectionKey} found locally", traceId, key);
            var response = await collection.ApplyAsync(operation, cancellationToken);
            // await SetShardSizeAsync(shardId, collection.Size, collection.StorageSize, traceId, propagateToNodes, cancellationToken);
            // _logger?.LogDebug("TraceId {TraceId}, shard {ShardId} found locally", traceId, shardId);
            return (TResponse) response;
        }

        var nodes = nodesWithShard.Value.Where(nodeId => nodeId != _thisNode.Id).ToList();
        if (nodes.Count == 0) return Response<TResponse>.Fail("Operation routed to zero shards");

        var routeTo = nodes[Random.Next(0, nodes.Count)];

        var channelManager = _channelManager
                             ?? ChannelManagerAccessor.Manager
                             ?? throw new InitializationException(
                                                                  "Operation can not be routed, as channel manager was not " +
                                                                  "passed in constructor or set in ChannelManagerAccessor");

        if (!channelManager.TryGet<IOperationPassingGrpcService<TOperation, TResponse>>(routeTo, out var client))
        {
            throw new GeneratedCodeExpectationsViolatedException(
                                                                 $"Could not get {typeof(IOperationPassingGrpcService<TOperation, TResponse>)}");
        }

        // _logger?.LogDebug("TraceId {TraceId}, operation was re-routed to node {NodeId}", traceId, routeTo);
        return await client.ApplyAsync(
                                       new CrdtOperation<TOperation>(TypeName, InstanceId, traceId, shardId, operation),
                                       cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<MergeResult> MergeAsync(
        PartiallyReplicatedCrdtRegistryDto other,
        CancellationToken cancellationToken = default
    )
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        _metricsRegistry?.RecordMergeTrigger(TypeName);

        var conflict = false;
        var infosMerge = MergeResult.Identical;
        try
        {
            infosMerge = _collectionInfos.MaybeMerge(other.CollectionInfos);
            var stateMerge = _currentState.MaybeMerge(other.CurrentState);
            conflict = infosMerge == MergeResult.ConflictSolved
                       || stateMerge == MergeResult.ConflictSolved;
        }
        finally
        {
            _semaphore.Release();
        }

        if (infosMerge == MergeResult.ConflictSolved) await RebalanceAsync(cancellationToken: cancellationToken);
        MaybeUpdateCurrentState();

        return conflict ? MergeResult.ConflictSolved : MergeResult.Identical;
    }

    /// <inheritdoc />
    public override async Task<PartiallyReplicatedCrdtRegistryDto> ToDtoAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            var hashableCrdtRegistryDto = _collectionInfos.ToDto();

            _metricsRegistry?.CollectDtoSize(hashableCrdtRegistryDto.Dict?.Count, TypeName);

            return new PartiallyReplicatedCrdtRegistryDto
            {
                CollectionInfos = hashableCrdtRegistryDto,
                CurrentState = _currentState.ToDto()
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<PartiallyReplicatedCrdtRegistryDto> EnumerateDtoBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        yield return await ToDtoAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_collectionInfos, _currentState);

    /// <inheritdoc />
    Task IReactToOtherCrdtChange.HandleChangeInAnotherCrdtAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken
    )
        => RebalanceAsync(cancellationToken: cancellationToken);

    private async Task RebalanceAsync(string traceId = "", CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException($"TraceId {traceId}: Deadlock");
        }

        try
        {
            var allShards = new Dictionary<ShardId, ulong>();
            foreach (var (shardId, size) in _collectionInfos.Values.SelectMany(info => info.Shards.Dict))
            {
                allShards.Add(shardId, size.StorageSize);
            }

            var desiredDistribution = _partialReplicationStrategy
                .GetDistribution(allShards, ManagedCrdtContext.Nodes.Value);

            if (!desiredDistribution.OrderedEquals(_desiredDistribution))
            {
                _logger?.LogDebug("TraceId {TraceId}: new desired distribution {DesiredDistribution}",
                                  traceId, JsonConvert.SerializeObject(desiredDistribution));
            }

            // If some node(s) went down, remove it/them from current state.
            foreach (var shardId in _currentState.Keys)
            {
                if (!_currentState.TryGetValue(shardId, out var collection)) continue;

                foreach (var removedNode in collection.Value.Except(ManagedCrdtContext
                                                                    .Nodes.Value.Select(ni => ni.Id)))
                {
                    collection.Remove(removedNode);
                }
            }

            // If collection is required to be on this node, create it. It will be populated by Consistency service
            // Notice that if collection is no longer required to be on this node, we do NOT delete it here.
            // Instead it will be synced and deleted by consistency service
            foreach (var (shardId, nodeInfos) in desiredDistribution)
            {
                if (!nodeInfos.Contains(_thisNode) || _collections.ContainsKey(shardId)) continue;
                await CreateLocalShardAsync(shardId, traceId, cancellationToken);
            }

            _desiredDistribution = desiredDistribution;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task CreateLocalShardAsync(ShardId shardId, string traceId, CancellationToken cancellationToken)
    {
        if (!TryGetCollectionKey(shardId, out var key))
            throw new KeyNotFoundException(
                                           $"TraceId {traceId}: ShardId {shardId} does not belong to any known collection");

        var managedCrdt = _factory.Create(InstanceId.FromShardId(shardId));
        foreach (var indexName in _collectionInfos[key].IndexNames.Value)
        {
            if (IndexFactory.TryGetIndex<IIndex<TCollectionKey, TCollectionValue>>(indexName, out var index))
            {
                await managedCrdt.AddIndexAsync(index, cancellationToken);
                _logger?.LogInformation("TraceId {TraceId}: Index {IndexName} added to shard {ShardId}",
                                        traceId, indexName, shardId);
            }
            else
            {
                _logger?.LogError("TraceId {TraceId}: shard {ShardId} should contain index " +
                                  "{IndexName}, but index was not registered in IndexFactory",
                                  traceId, shardId, indexName);
            }
        }

        _collections.TryAdd(shardId, managedCrdt);
        ManagedCrdtContext.Add<TCollection, TCollectionDto>(managedCrdt, this, this);
    }

    private bool TryGetCollectionKey(ShardId shardId, [NotNullWhen(true)] out TKey? collectionKey)
    {
        collectionKey = _cache.GetOrCreate(shardId, entry =>
        {
            foreach (var key in _collectionInfos.Keys)
            {
                if (!_collectionInfos.TryGetValue(key, out var info)) continue;
                if (info.Shards.ContainsKey(shardId))
                {
                    entry.SlidingExpiration = TimeSpan.FromHours(1);
                    return key;
                }
            }

            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
            return default;
        });
        return collectionKey != null;
    }

    private void MaybeUpdateCurrentState()
    {
        foreach (var (shardId, collection) in _collections)
        {
            if (!TryGetCollectionKey(shardId, out var key)
                || !_collectionInfos.TryGetValue(key, out var collectionInfo)
                || collection.Size != collectionInfo.Shards[shardId].Size
                || collection.StorageSize != collectionInfo.Shards[shardId].StorageSize) continue;

            if (!_currentState.TryGetValue(shardId, out var nodes))
            {
                nodes = new HashableOptimizedObservedRemoveSet<NodeId, NodeId>();
                _currentState.TryAdd(_thisNode.Id, shardId, nodes);
            }

            if (!nodes.Contains(_thisNode.Id)) nodes.Add(_thisNode.Id, _thisNode.Id);
        }
    }

    /// <inheritdoc />
    async Task ICreateAndDeleteManagedCrdtsInside.MarkForDeletionLocallyAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken
    )
    {
        var shardId = ShardId.FromInstanceId(instanceId);
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new NyrisException("Deadlock");
        }

        try
        {
            _logger?.LogDebug("Shard with instance id {ShardId} is being removed locally", shardId);
            _collections.TryRemove(shardId, out _);
            if (_currentState.TryGetValue(shardId, out var set))
            {
                set.Remove(_thisNode.Id);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    IList<NodeInfo> INodesWithReplicaProvider.GetNodesThatShouldHaveReplicaOfCollection(InstanceId instanceId)
    {
        var shardId = ShardId.FromInstanceId(instanceId);
        if (_desiredDistribution.TryGetValue(shardId, out var nodes)) return nodes;

        _logger?.LogWarning("Requested list of nodes for shard {ShardId}, but it is not found", shardId);
        return ArraySegment<NodeInfo>.Empty;
    }

    private async Task SetShardSizeAsync(
        ShardId shardId,
        ulong size,
        ulong storageSize,
        string traceId,
        uint propagateToNodes,
        CancellationToken cancellationToken
    )
    {
        if (TryGetCollectionKey(shardId, out var key)
            && _collectionInfos.TryGetValue(key, out var info)
            && info.Shards.TryGetValue(shardId, out var shardSizes)
            && (shardSizes.Size != size || shardSizes.StorageSize != storageSize))
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                _logger?.LogDebug("Setting size of {ShardId} during refresh to {Size} ({StorageSize})",
                                  shardId, size, storageSize);
                // TODO: this allocation is unnecessary, should be removed
                info.Shards[shardId] = new ShardSizes(size, storageSize);
            }
            finally
            {
                _semaphore.Release();
            }

            await StateChangedAsync(propagateToNodes, false, traceId, cancellationToken);
            await RebalanceAsync(traceId, cancellationToken);
        }
    }

    private async Task RefreshShardSizesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            try
            {
                await TryRefreshShardSizesAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Unhandled exception during trying to refresh collection sizes");
            }
        }
    }

    internal async Task TryRefreshShardSizesAsync(CancellationToken cancellationToken)
    {
        foreach (var shardId in _collections.Keys)
        {
            if (_currentState.TryGetValue(shardId, out var nodes)
                && nodes.Contains(_thisNode.Id)
                && _collections.TryGetValue(shardId, out var collection))
            {
                _metricsRegistry?.CollectCollectionSize((int?) collection.Size, TypeName);

                await SetShardSizeAsync(shardId,
                                        collection.Size,
                                        collection.StorageSize,
                                        "shard-size-refresh",
                                        0,
                                        cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (!isDisposing) return;

        _cts.Cancel();
        _cts.Dispose();
        _cache.Dispose();
        _refreshShardSizesTask.Dispose();
        _semaphore.Dispose();
    }

    [ProtoContract]
    public sealed class PartiallyReplicatedCrdtRegistryDto
    {
        [ProtoMember(1)]
        public HashableCrdtRegistry<NodeId,
            TKey,
            CollectionInfo,
            CollectionInfo.CollectionInfoDto,
            CollectionInfo.CollectionInfoFactory>.HashableCrdtRegistryDto? CollectionInfos { get; set; }

        [ProtoMember(2)]
        public HashableCrdtRegistry<NodeId,
            ShardId,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory>.HashableCrdtRegistryDto? CurrentState { get; set; }
    }
}
