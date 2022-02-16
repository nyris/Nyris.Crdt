using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
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
            INodesWithReplicaProvider
        where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
        where TCollection : ManagedCrdtRegistry<TCollectionKey, TCollectionValue, TCollectionDto>,
            IAcceptOperations<TCollectionOperationBase, TCollectionOperationResponseBase>
        where TCollectionOperationBase : Operation
        where TCollectionOperationResponseBase : OperationResponse
        where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionDto>, new()
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Random Random = new();
        private readonly TCollectionFactory _factory;

        private readonly ILogger? _logger;

        private readonly NodeInfo _thisNode;
        private readonly IPartialReplicationStrategy _partialReplicationStrategy;
        private readonly SemaphoreSlim _semaphore = new(1);
        private ManagedCrdtContext? _context;

        private IDictionary<TKey, IList<NodeInfo>> _desiredDistribution;
        private readonly ConcurrentDictionary<TKey, TCollection> _collections;

        /// <summary>
        /// Keys of all collections, regardless if they are stored locally or not
        /// </summary>
        private readonly HashableCrdtRegistry<NodeId,
            TKey,
            CollectionInfo,
            CollectionInfo.CollectionInfoDto,
            CollectionInfo.CollectionInfoFactory> _collectionInfos;

        private readonly HashableLastWriteWinsRegistry<string, TKey, DateTime> _instanceIdToKeys;

        /// <summary>
        /// Mapping: key -> HashSet{NodeId}: shows which node contains which collections. Used for routing requests
        /// </summary>
        private readonly HashableCrdtRegistry<NodeId,
            TKey,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory> _currentState;

        /// <inheritdoc />
        protected PartiallyReplicatedCRDTRegistry(string instanceId,
            ILogger? logger = null,
            IPartialReplicationStrategy? partialReplicationStrategy = null,
            INodeInfoProvider? nodeInfoProvider = null,
            TCollectionFactory? factory = default) : base(instanceId, logger: logger)
        {
            _logger = logger;
            _thisNode = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
            _partialReplicationStrategy = partialReplicationStrategy ?? DefaultConfiguration.PartialReplicationStrategy;
            _collectionInfos = new HashableCrdtRegistry<NodeId, TKey, CollectionInfo, CollectionInfo.CollectionInfoDto, CollectionInfo.CollectionInfoFactory>();
            _currentState = new HashableCrdtRegistry<NodeId, TKey, HashableOptimizedObservedRemoveSet<NodeId, NodeId>, OptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto, HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory>();
            _collections = new ConcurrentDictionary<TKey, TCollection>();
            _instanceIdToKeys = new HashableLastWriteWinsRegistry<string, TKey, DateTime>();
            _desiredDistribution = new Dictionary<TKey, IList<NodeInfo>>();
            _factory = factory ?? new TCollectionFactory();
        }

        /// <inheritdoc />
        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        public async Task<bool> TryAddCollectionAsync(TKey key, string instanceId,
            IEnumerable<string>? indexNames = null,
            int waitForPropagationToNumNodes = 0,
            string traceId = "",
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // TODO: can one of those fail? What happens in that case?
                return _collectionInfos.TryAdd(_thisNode.Id, key, new CollectionInfo(indexes: indexNames)) &
                       _instanceIdToKeys.TrySet(instanceId, key, DateTime.UtcNow, out _);
            }
            finally
            {
                _semaphore.Release();
                await RebalanceAsync(traceId: traceId, cancellationToken: cancellationToken);
                await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes,
                    traceId: traceId,
                    cancellationToken: cancellationToken);
                _logger?.LogDebug("TraceId: {TraceId}, changes to registry propagated, end of {FuncName}",
                    traceId, nameof(TryAddCollectionAsync));
            }
        }

        public async Task<bool> TryRemoveCollectionAsync(TKey key,
            int waitForPropagationToNumNodes = 0,
            string traceId = "",
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _currentState.Remove(key);
                _collectionInfos.Remove(key);
                if (_collections.TryRemove(key, out var collection))
                {
                    _instanceIdToKeys.TryRemove(collection.InstanceId, DateTime.UtcNow, out _);
                    ManagedCrdtContext.Remove<TCollection, TCollectionDto>(collection);
                }
                return true;
            }
            finally
            {
                _semaphore.Release();
                await RebalanceAsync(traceId: traceId, cancellationToken: cancellationToken);
                await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes,
                    traceId: traceId,
                    cancellationToken: cancellationToken);
            }
        }

        public bool CollectionExists(TKey key) =>
            _collectionInfos.TryGetValue(key, out _)
            && _currentState.TryGetValue(key, out _)
            && _instanceIdToKeys.Values.Contains(key);

        public bool TryGetCollectionSize(TKey key, out ulong size)
        {
            if (!_collectionInfos.TryGetValue(key, out var collectionInfo))
            {
                size = 0;
                return false;
            }
            size = collectionInfo.Size;
            return true;
        }

        public async Task<TResponse> ApplyAsync<TOperation, TResponse>(TKey key,
            TOperation operation,
            string traceId = "",
            CancellationToken cancellationToken = default)
            where TOperation : TCollectionOperationBase
            where TResponse : TCollectionOperationResponseBase
        {
            _logger?.LogDebug("TraceId {TraceId}, registry received an operation for collection " +
                              "{CollectionKey} of type {OperationType}", traceId, key, typeof(TOperation));
            if (!_currentState.TryGetValue(key, out var nodesWithCollection))
            {
                // TODO: specify a NotFound response?
                throw new KeyNotFoundException($"{key} was not found in the current state");
            }

            if (nodesWithCollection.Contains(_thisNode.Id) && _collections.TryGetValue(key, out var collection))
            {
                // _logger?.LogDebug("TraceId {TraceId}, collection {CollectionKey} found locally", traceId, key);
                var response = await collection.ApplyAsync(operation, cancellationToken);
                await SetCollectionSizeAsync(key, collection.Size, traceId, cancellationToken);
                _logger?.LogDebug("TraceId {TraceId}, collection {CollectionKey} found locally, new size is {Size}",
                    traceId, key, _collectionInfos[key].Size);
                return (TResponse)response;
            }

            var nodes = nodesWithCollection.Value.ToList();
            var routeTo = nodes[Random.Next(0, nodes.Count)];
            var channelManager = ChannelManagerAccessor.Manager ?? throw new InitializationException(
                "Operation can not be routed to a different node - Channel manager was not instantiated yet");

            if (!channelManager.TryGet<IOperationPassingGrpcService<TOperation, TResponse, TKey>>(
                    routeTo, out var client))
            {
                throw new GeneratedCodeExpectationsViolatedException(
                    $"Could not get {typeof(IOperationPassingGrpcService<TOperation, TResponse, TKey>)}");
            }

            _logger?.LogDebug("TraceId {TraceId}, operation was re-routed to node {NodeId}", traceId, routeTo);
            return await client.ApplyAsync(
                new CrdtOperation<TOperation, TKey>(TypeName, InstanceId, traceId, key, operation),
                cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(PartiallyReplicatedCrdtRegistryDto other, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            var conflict = false;
            var keysMerge = MergeResult.Identical;
            try
            {
                keysMerge = _collectionInfos.MaybeMerge(other.CollectionInfos);
                var stateMerge = _currentState.MaybeMerge(other.CurrentState);
                var mappingMerge = _instanceIdToKeys.MaybeMerge(other.InstanceIdToKeys);
                conflict = keysMerge == MergeResult.ConflictSolved
                           || stateMerge == MergeResult.ConflictSolved
                           || mappingMerge == MergeResult.ConflictSolved;
            }
            finally
            {
                _semaphore.Release();
                if(keysMerge == MergeResult.ConflictSolved) await RebalanceAsync(cancellationToken: cancellationToken);
                MaybeUpdateCurrentState();
            }

            return conflict ? MergeResult.ConflictSolved : MergeResult.Identical;
        }

        /// <inheritdoc />
        public override async Task<PartiallyReplicatedCrdtRegistryDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return new PartiallyReplicatedCrdtRegistryDto
                {
                    CollectionInfos = _collectionInfos.ToDto(),
                    CurrentState = _currentState.ToDto(),
                    InstanceIdToKeys = _instanceIdToKeys.ToDto()
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<PartiallyReplicatedCrdtRegistryDto> EnumerateDtoBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await ToDtoAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_collectionInfos, _currentState, _instanceIdToKeys);

        /// <inheritdoc />
        Task IReactToOtherCrdtChange.HandleChangeInAnotherCrdtAsync(string instanceId, CancellationToken cancellationToken)
        {
            // if (_instanceIdToKeys.TryGetValue(instanceId, out var key)
            //     && _collectionInfos.TryGetValue(key, out var collectionInfo)
            //     && _collections.TryGetValue(key, out var collection)
            //     && _currentState.TryGetValue(key, out var nodes))
            // {
            //     collectionInfo.Size = collection.Size;
            // }
            return RebalanceAsync(cancellationToken: cancellationToken);
        }

        private async Task RebalanceAsync(string traceId = "", CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var desiredDistribution = _partialReplicationStrategy
                    .GetDistribution(_collectionInfos.Value(info => info.Size), ManagedCrdtContext.Nodes.Value);
                _logger?.LogDebug("TraceId {TraceId}: new desired distribution {DesiredDistribution}",
                    traceId, JsonConvert.SerializeObject(desiredDistribution));

                // If some node(s) went down, remove it/them from current state.
                foreach (var collectionKey in _currentState.Keys)
                {
                    if (!_currentState.TryGetValue(collectionKey, out var collection)) continue;

                    foreach (var removedNode in collection.Value.Except(
                                 ManagedCrdtContext.Nodes.Value.Select(ni => ni.Id)))
                    {
                        collection.Remove(removedNode);
                    }
                }

                // If collection is required to be on this node, create it. It will be populated by Consistency service
                // Notice that if collection is no longer required to be on this node, we do NOT delete it here.
                // Instead it will be synced and deleted by consistency service
                foreach (var (key, nodeInfos) in desiredDistribution)
                {
                    if (!nodeInfos.Contains(_thisNode) || _collections.ContainsKey(key)) continue;

                    var instanceId = _instanceIdToKeys.Value.First(pair => pair.Value.Equals(key)).Key;
                    var managedCrdt = _factory.Create(instanceId);

                    foreach (var indexName in _collectionInfos[key].IndexNames.Value)
                    {
                        if (IndexFactory.TryGetIndex<IIndex<TCollectionKey, TCollectionValue>>(indexName,
                                out var index))
                        {
                            await managedCrdt.AddIndexAsync(index, cancellationToken);
                            _logger.LogInformation("Index {IndexName} added to collection {CollectionKey}",
                                indexName, key);
                        }
                        else
                        {
                            _logger.LogError("TraceId {TraceId}: collection {CollectionKey} should contain index " +
                                             "{IndexName}, but index was not registered in IndexFactory",
                                traceId, key, indexName);
                        }
                    }
                    _collections.TryAdd(key, managedCrdt);
                    ManagedCrdtContext.Add(managedCrdt, _factory, this, this);
                }

                _desiredDistribution = desiredDistribution;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void MaybeUpdateCurrentState()
        {
            // For each collection stored locally, we can add this node to a _currentState if that collection is up to date
            // A couple of words of explanation:
            // 1. _currentState holds a mapping from collectionIds to lists of nodes that store respective collections
            // 2. After new collection is created or NodeSet is changed, collections may need to rebalance - i.e. some
            // collections will be transferred between nodes. This is not an instantaneous process. So we may have
            // a situation where node A contain a freshly created local collection, that is in a process of syncing with
            // large collection on node B. In this case we want _currentState to contain mapping to node B, but node A.
            // To make this happen, we only add local collection to _currentState iff it's size is the same as the
            // one stored in _collectionInfos
            foreach (var (key, collection) in _collections)
            {
                if (!_collectionInfos.TryGetValue(key, out var collectionInfo)
                    || collection.Size != collectionInfo.Size) continue;

                if (!_currentState.TryGetValue(key, out var nodes))
                {
                    nodes = new HashableOptimizedObservedRemoveSet<NodeId, NodeId>();
                    _currentState.TryAdd(_thisNode.Id, key, nodes);
                }

                if(!nodes.Contains(_thisNode.Id)) nodes.Add(_thisNode.Id, _thisNode.Id);
            }
        }

        /// <inheritdoc />
        async Task ICreateAndDeleteManagedCrdtsInside.MarkForDeletionLocallyAsync(string instanceId, CancellationToken cancellationToken)
        {
            if (!_instanceIdToKeys.TryGetValue(instanceId, out var key)) return;
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogDebug("Collection with instance id {InstanceId} is being removed locally", instanceId);
                _collections.TryRemove(key, out _);
                if (_currentState.TryGetValue(key, out var set))
                {
                    set.Remove(_thisNode.Id);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        IList<NodeInfo> INodesWithReplicaProvider.GetNodesThatShouldHaveReplicaOfCollection(string instanceId)
        {
            if(!_instanceIdToKeys.TryGetValue(instanceId, out var key) ||
               !_desiredDistribution.TryGetValue(key, out var nodes))
            {
                return ArraySegment<NodeInfo>.Empty;
            }

            return nodes;
        }

        private async Task SetCollectionSizeAsync(TKey key, ulong size, string traceId,
            CancellationToken cancellationToken)
        {
            if (_collectionInfos.TryGetValue(key, out var info) && info.Size != size)
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    info.Size = size;
                }
                finally
                {
                    _semaphore.Release();
                    await StateChangedAsync(2, traceId, cancellationToken);
                }
            }
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
                TKey,
                HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
                HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
                HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory>.HashableCrdtRegistryDto? CurrentState { get; set; }

            [ProtoMember(3)]
            public HashableLastWriteWinsRegistry<string,
                TKey,
                DateTime>.LastWriteWinsRegistryDto? InstanceIdToKeys { get; set; }
        }
    }

}