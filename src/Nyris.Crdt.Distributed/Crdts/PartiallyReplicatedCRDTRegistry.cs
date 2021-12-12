using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Crdts
{
    /// <summary>
    /// The registry is a key -> collection mapping, where each collection is replicated to a subset of other nodes.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TCollection"></typeparam>
    /// <typeparam name="TDto"></typeparam>
    /// <typeparam name="TCollectionImplementation"></typeparam>
    /// <typeparam name="TCollectionRepresentation"></typeparam>
    /// <typeparam name="TCollectionDto"></typeparam>
    /// <typeparam name="TCollectionOperation"></typeparam>
    public abstract class PartiallyReplicatedCRDTRegistry<TKey, TCollection, TDto, TCollectionImplementation, TCollectionRepresentation, TCollectionDto, TCollectionOperation>
        : ManagedCRDT<PartiallyReplicatedCRDTRegistry<TKey, TCollection, TDto, TCollectionImplementation, TCollectionRepresentation, TCollectionDto, TCollectionOperation>,
                Dictionary<TKey, TCollection>,
                TDto>,
            ICreateManagedCrdtsInside
        where TKey : IEquatable<TKey>, IHashable
        where TCollection : ManageCRDTWithSerializableOperations<TCollectionImplementation, TCollectionRepresentation, TCollectionDto, TCollectionOperation>
        where TCollectionImplementation : ManagedCRDT<TCollectionImplementation, TCollectionRepresentation, TCollectionDto>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Random Random = new();

        private ManagedCrdtContext? _context;

        /// <summary>
        /// Keys of all collections, regardless if they are stored locally or not
        /// </summary>
        private readonly HashableOptimizedObservedRemoveSet<NodeId, TKey> _keys = new();

        /// <summary>
        /// Mapping: key -> HashSet{NodeId}: shows which node contains which collections. Used for routing requests
        /// </summary>
        private readonly CrdtRegistry<NodeId,
            TKey,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto,
            HashableOptimizedObservedRemoveSet<NodeId, NodeId>.Factory,
            HashSet<NodeId>> _currentState = new();

        private readonly ConcurrentDictionary<TKey, TCollection> _collections = new();

        /// <inheritdoc />
        protected PartiallyReplicatedCRDTRegistry(string instanceId) : base(instanceId)
        {
        }

        /// <inheritdoc />
        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        /// <inheritdoc />
        public override Task<MergeResult> MergeAsync(PartiallyReplicatedCRDTRegistry<TKey, TCollection, TDto, TCollectionImplementation, TCollectionRepresentation, TCollectionDto, TCollectionOperation> other, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override Task<TDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash()
        {
            throw new NotImplementedException();
        }

        public void CreateCollection(TKey key)
        {
            
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
    }

}