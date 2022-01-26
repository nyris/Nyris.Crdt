using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Exceptions;
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
    /// <typeparam name="TCollectionRepresentation"></typeparam>
    /// <typeparam name="TCollectionDto"></typeparam>
    /// <typeparam name="TCollectionOperationBase"></typeparam>
    /// <typeparam name="TCollectionFactory"></typeparam>
    /// <typeparam name="TCollectionOperationResponseBase"></typeparam>
    /// <typeparam name="TImplementation"></typeparam>
    public abstract class PartiallyReplicatedCRDTRegistry<TImplementation,
            TKey,
            TCollection,
            TCollectionRepresentation,
            TCollectionDto,
            TCollectionOperationBase,
            TCollectionOperationResponseBase,
            TCollectionFactory>
        : ManagedCRDT<TImplementation,
                IReadOnlyDictionary<TKey, TCollectionRepresentation>,
                PartiallyReplicatedCRDTRegistry<TImplementation,
                    TKey,
                    TCollection,
                    TCollectionRepresentation,
                    TCollectionDto,
                    TCollectionOperationBase,
                    TCollectionOperationResponseBase,
                    TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>,
            ICreateAndDeleteManagedCrdtsInside,
            IRebalanceAtNodeChange,
            INodesWithReplicaProvider
        where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
        where TCollection : ManagedCRDTWithSerializableOperations<TCollection,
            TCollectionRepresentation,
            TCollectionDto,
            TCollectionOperationBase,
            TCollectionOperationResponseBase>
        where TCollectionOperationBase : Operation
        where TCollectionOperationResponseBase : OperationResponse
        where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionRepresentation, TCollectionDto>, new()
        where TImplementation : PartiallyReplicatedCRDTRegistry<TImplementation,
            TKey,
            TCollection,
            TCollectionRepresentation,
            TCollectionDto,
            TCollectionOperationBase,
            TCollectionOperationResponseBase,
            TCollectionFactory>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Random Random = new();
        private static readonly TCollectionFactory Factory = new();

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
            CollectionSize,
            CollectionSize.CollectionSizeDto,
            CollectionSize.CollectionSizeFactory,
            ulong> _collectionInfos;

        private readonly HashableLastWriteWinsRegistry<string, TKey, DateTime> _instanceIdToKeys;

        /// <summary>
        /// Mapping: key -> HashSet{NodeId}: shows which node contains which collections. Used for routing requests
        /// </summary>
        private readonly HashableCrdtRegistry<NodeId,
            TKey,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory,
            HashSet<NodeId>> _currentState;

        /// <inheritdoc />
        protected PartiallyReplicatedCRDTRegistry(string instanceId,
            IPartialReplicationStrategy? shardingStrategy = null,
            INodeInfoProvider? nodeInfoProvider = null) : base(instanceId)
        {
            _thisNode = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
            _partialReplicationStrategy = shardingStrategy ?? DefaultConfiguration.PartialReplicationStrategy;
            _collectionInfos = new HashableCrdtRegistry<NodeId, TKey, CollectionSize, CollectionSize.CollectionSizeDto, CollectionSize.CollectionSizeFactory, ulong>();
            _currentState = new HashableCrdtRegistry<NodeId, TKey, HashableOptimizedObservedRemoveSet<NodeId, NodeId>, OptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto, HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory, HashSet<NodeId>>();
            _collections = new ConcurrentDictionary<TKey, TCollection>();
            _instanceIdToKeys = new HashableLastWriteWinsRegistry<string, TKey, DateTime>();
            _desiredDistribution = new Dictionary<TKey, IList<NodeInfo>>();
        }

        protected PartiallyReplicatedCRDTRegistry(PartiallyReplicatedCrdtRegistryDto dto,
            string instanceId,
            IPartialReplicationStrategy? shardingStrategy = null,
            INodeInfoProvider? nodeInfoProvider = null) : base(instanceId)
        {
            _thisNode = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
            _collectionInfos = new HashableCrdtRegistry<NodeId, TKey, CollectionSize, CollectionSize.CollectionSizeDto,
                CollectionSize.CollectionSizeFactory, ulong>(dto.Keys);
            _currentState =
                new HashableCrdtRegistry<NodeId, TKey, HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
                    OptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
                    HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory, HashSet<NodeId>>(dto.CurrentState);
            _instanceIdToKeys = new HashableLastWriteWinsRegistry<string, TKey, DateTime>(dto.InstanceIdToKeys);

            // should be irrelevant
            _collections = new ConcurrentDictionary<TKey, TCollection>();
            _partialReplicationStrategy = shardingStrategy ?? DefaultConfiguration.PartialReplicationStrategy;
            _desiredDistribution = new Dictionary<TKey, IList<NodeInfo>>();
        }

        /// <summary>
        /// Partially replicated registry can not provide a direct representation
        /// (since only part of the data is local), so this will throw an exception.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public sealed override Dictionary<TKey, TCollectionRepresentation> Value => throw new NotImplementedException(
            "Partially replicated registry can not provide a direct representation");

        /// <inheritdoc />
        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        /// <inheritdoc />
        async Task ICreateAndDeleteManagedCrdtsInside.MarkForDeletionAsync(string instanceId, CancellationToken cancellationToken)
        {
            if (!_instanceIdToKeys.TryGetValue(instanceId, out var key)) return;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _instanceIdToKeys.TryRemove(instanceId, DateTime.UtcNow, out _);
                _collectionInfos.Remove(key);
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

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(TImplementation other, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            var conflict = false;
            var keysMerge = MergeResult.Identical;
            try
            {
                keysMerge = _collectionInfos.Merge(other._collectionInfos);
                var stateMerge = _currentState.Merge(other._currentState);
                var mappingMerge = _instanceIdToKeys.Merge(other._instanceIdToKeys);
                conflict = keysMerge == MergeResult.ConflictSolved
                           || stateMerge == MergeResult.ConflictSolved
                           || mappingMerge == MergeResult.ConflictSolved;
            }
            finally
            {
                _semaphore.Release();
                if(keysMerge == MergeResult.ConflictSolved) await RebalanceAsync();
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
                    Keys = _collectionInfos.ToDto(),
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

        public async Task<bool> TryAddCollectionAsync(TKey key, TCollection collection, int waitForPropagationToNumNodes = 0)
        {
            await _semaphore.WaitAsync();
            try
            {
                // TODO: do we need a proper synchronization between _keys _collection and _instanceIdToKeys guarantees here?
                if (!_collectionInfos.TryAdd(_thisNode.Id, key, new CollectionSize()) ||
                    !_collections.TryAdd(key, collection))
                {
                    return false;
                }

                _instanceIdToKeys.TrySet(collection.InstanceId, key, DateTime.UtcNow, out _);
                ManagedCrdtContext.Add(collection, Factory, this, this);
                return true;
            }
            finally
            {
                _semaphore.Release();
                await RebalanceAsync();
                await StateChangedAsync(waitForPropagationToNumNodes);
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
            size = collectionInfo.Value;
            return true;
        }

        Task IRebalanceAtNodeChange.RebalanceAsync() => RebalanceAsync();

        private async Task RebalanceAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var desiredDistribution = _partialReplicationStrategy
                    .GetDistribution(_collectionInfos.Value, ManagedCrdtContext.Nodes.Value);

                foreach (var (key, nodeInfos) in desiredDistribution)
                {
                    if (nodeInfos.Contains(_thisNode) && !_collections.ContainsKey(key))
                    {
                        var instanceId = _instanceIdToKeys.Value.First(pair => pair.Value.Equals(key)).Key;
                        var managedCrdt = Factory.Create(instanceId);
                        _collections.TryAdd(key, managedCrdt);
                        ManagedCrdtContext.Add(managedCrdt, Factory, this, this);
                    }
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
            foreach (var (key, collection) in _collections)
            {
                if (!_collectionInfos.TryGetValue(key, out var collectionSize)) continue;
                if (collection.Size != collectionSize.Value) continue;

                if (!_currentState.TryGetValue(key, out var nodes))
                {
                    nodes = new HashableOptimizedObservedRemoveSet<NodeId, NodeId>();
                    _currentState.TryAdd(_thisNode.Id, key, nodes);
                }

                if(!nodes.Contains(_thisNode.Id)) nodes.Add(_thisNode.Id, _thisNode.Id);
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

        public async Task<TResponse> ApplyAsync<TOperation, TResponse>(TKey key, TOperation operation, CancellationToken cancellationToken = default)
            where TOperation : TCollectionOperationBase
            where TResponse : TCollectionOperationResponseBase
        {
            if (!_currentState.TryGetValue(key, out var nodesWithCollection))
            {
                // TODO: specify a NotFound response?
                throw new KeyNotFoundException();
            }

            if (nodesWithCollection.Contains(_thisNode.Id))
            {
                return (TResponse) await _collections[key].ApplyAsync(operation);
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

            return await client.ApplyAsync(
                new CrdtOperation<TOperation, TKey>(TypeName, InstanceId, key, operation),
                cancellationToken);
        }

        [ProtoContract]
        public sealed class PartiallyReplicatedCrdtRegistryDto
        {
            [ProtoMember(1)]
            public HashableCrdtRegistry<NodeId,
                TKey,
                CollectionSize,
                CollectionSize.CollectionSizeDto,
                CollectionSize.CollectionSizeFactory,
                ulong>.HashableCrdtRegistryDto Keys { get; set; } = new();

            [ProtoMember(2)]
            public HashableCrdtRegistry<NodeId,
                TKey,
                HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
                HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
                HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory,
                HashSet<NodeId>>.HashableCrdtRegistryDto CurrentState { get; set; } = new();

            [ProtoMember(3)]
            public HashableLastWriteWinsRegistry<string,
                TKey,
                DateTime>.LastWriteWinsRegistryDto InstanceIdToKeys { get; set; } = new();
        }
    }

}