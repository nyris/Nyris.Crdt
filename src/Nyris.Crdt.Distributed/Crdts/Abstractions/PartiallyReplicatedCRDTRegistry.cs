using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
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
    /// <typeparam name="TDto"></typeparam>
    /// <typeparam name="TCollectionRepresentation"></typeparam>
    /// <typeparam name="TCollectionDto"></typeparam>
    /// <typeparam name="TCollectionOperation"></typeparam>
    /// <typeparam name="TImplementation"></typeparam>
    /// <typeparam name="TCollectionFactory"></typeparam>
    public abstract class PartiallyReplicatedCRDTRegistry<TImplementation,
            TKey,
            TCollection,
            TCollectionRepresentation,
            TCollectionDto,
            TCollectionOperation,
            TCollectionFactory>
        : ManagedCRDT<TImplementation, Dictionary<TKey, TCollection>, PartiallyReplicatedCRDTRegistry<TImplementation,
                TKey,
                TCollection,
                TCollectionRepresentation,
                TCollectionDto,
                TCollectionOperation,
                TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>,
            ICreateManagedCrdtsInside,
            IRebalanceAtNodeChange,
            INodesWithReplicaProvider
        where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
        where TCollection : ManagedCRDTWithSerializableOperations<TCollection, TCollectionRepresentation, TCollectionDto, TCollectionOperation>
        where TImplementation : PartiallyReplicatedCRDTRegistry<TImplementation, TKey, TCollection, TCollectionRepresentation, TCollectionDto, TCollectionOperation, TCollectionFactory>
        where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionRepresentation, TCollectionDto>, new()
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Random Random = new();
        // ReSharper disable once StaticMemberInGenericType
        private static readonly NodeInfo ThisNode = NodeInfoProvider.GetMyNodeInfo();
        private static readonly TCollectionFactory Factory = new();

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
            ulong> _keys;

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
        protected PartiallyReplicatedCRDTRegistry(string instanceId, IPartialReplicationStrategy? shardingStrategy = null) : base(instanceId)
        {
            _partialReplicationStrategy = shardingStrategy ?? DefaultConfiguration.PartialReplicationStrategy;
            _keys = new HashableCrdtRegistry<NodeId, TKey, CollectionSize, CollectionSize.CollectionSizeDto, CollectionSize.CollectionSizeFactory, ulong>();
            _currentState = new HashableCrdtRegistry<NodeId, TKey, HashableOptimizedObservedRemoveSet<NodeId, NodeId>, OptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto, HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory, HashSet<NodeId>>();
            _collections = new ConcurrentDictionary<TKey, TCollection>();
            _instanceIdToKeys = new HashableLastWriteWinsRegistry<string, TKey, DateTime>();
            _desiredDistribution = new Dictionary<TKey, IList<NodeInfo>>();
        }

        /// <summary>
        /// Partially replicated registry can not provide a direct representation
        /// (since only part of the data is local), so this will throw an exception.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public sealed override Dictionary<TKey, TCollection> Value => throw new NotImplementedException(
            "Partially replicated registry can not provide a direct representation");

        /// <inheritdoc />
        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(TImplementation other, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            var conflict = false;
            var keysMerge = MergeResult.Identical;
            try
            {
                keysMerge = _keys.Merge(other._keys);
                var stateMerge = _currentState.Merge(other._currentState);
                conflict = keysMerge == MergeResult.ConflictSolved || stateMerge == MergeResult.ConflictSolved;
            }
            finally
            {
                _semaphore.Release();
                if(conflict) await StateChangedAsync();
                if(keysMerge == MergeResult.ConflictSolved)Rebalance();
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
                    Keys = _keys.ToDto(),
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
        public override ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_keys, _currentState, _instanceIdToKeys);

        public async Task<bool> TryAddCollectionAsync(TKey key, TCollection collection)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!_keys.TryAdd(NodeInfoProvider.ThisNodeId, key, new CollectionSize()) ||
                    !_collections.TryAdd(key, collection))
                {
                    return false;
                }

                _instanceIdToKeys.TrySet(collection.InstanceId, key, DateTime.UtcNow, out _);
                ManagedCrdtContext.Add(collection, Factory, this);
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        void IRebalanceAtNodeChange.Rebalance() => Rebalance();

        private void Rebalance()
        {
            var desiredDistribution = _partialReplicationStrategy.GetDistribution(_keys.Value, ManagedCrdtContext.Nodes.Value);
            foreach (var (key, nodeInfos) in desiredDistribution)
            {
                if (nodeInfos.Contains(ThisNode) && !_collections.ContainsKey(key))
                {
                    var instanceId = _instanceIdToKeys.Value.First(pair => pair.Value.Equals(key)).Key;
                    _collections.TryAdd(key, Factory.Create(instanceId));
                }
            }

            _desiredDistribution = desiredDistribution;
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

        public async Task ApplyAsync(TKey key, TCollectionOperation operation, CancellationToken cancellationToken = default)
        {
            if (!_currentState.TryGetValue(key, out var nodesWithCollection))
            {
                // TODO: specify a NotFound response
                return;
            }

            if (nodesWithCollection.Contains(NodeInfoProvider.ThisNodeId))
            {
                await _collections[key].ApplyAsync(operation);
            }
            else
            {
                var nodes = nodesWithCollection.Value.ToList();
                var routeTo = nodes[Random.Next(0, nodes.Count)];
                var channelManager = ChannelManagerAccessor.Manager ?? throw new InitializationException(
                    "Operation can not be routed to a different node - Channel manager was not instantiated yet");

                if (channelManager.TryGet<IOperationPassingGrpcService<TCollectionOperation, TKey>>(
                        routeTo, out var client))
                {
                    await client.ApplyAsync(
                        new CrdtOperation<TCollectionOperation, TKey>(TypeName, InstanceId, key, operation),
                        cancellationToken);
                }
            }
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