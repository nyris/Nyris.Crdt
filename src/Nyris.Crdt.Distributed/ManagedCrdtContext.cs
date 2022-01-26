using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCrdtContext
    {
        private readonly ConcurrentDictionary<TypeAndInstanceId, object> _managedCrdts = new();
        private readonly ConcurrentDictionary<Type, object> _crdtFactories = new();
        private readonly ConcurrentDictionary<TypeNameAndInstanceId, IHashable> _sameManagedCrdts = new();
        private readonly ConcurrentDictionary<Type, HashSet<TypeNameAndInstanceId>> _typeToTypeNameMapping = new();

        private readonly ConcurrentDictionary<TypeNameAndInstanceId, INodesWithReplicaProvider> _partiallyReplicated = new();
        private readonly ConcurrentDictionary<TypeNameAndInstanceId, ICreateAndDeleteManagedCrdtsInside> _holders = new();

        internal NodeSet Nodes { get; } = new("nodes_internal");

        protected ManagedCrdtContext()
        {
            Add(Nodes, NodeSet.DefaultFactory);
        }

        protected internal void Add<TCrdt, TRepresentation, TDto>(TCrdt crdt,
            IManagedCRDTFactory<TCrdt, TRepresentation, TDto> factory)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            if (typeof(TCrdt).IsGenericType)
            {
                throw new ManagedCrdtContextSetupException("Only non generic types can be used in a managed " +
                                                           "context (because library needs to generate concrete grpc " +
                                                           "messaged based on dto type used)." +
                                                           $"Try creating concrete type, that inherits from {typeof(TCrdt)}");
            }

            var typeNameAndInstanceId = new TypeNameAndInstanceId(TypeNameCompressor.GetName<TCrdt>(), crdt.InstanceId);
            var typeAndInstanceId = new TypeAndInstanceId(typeof(TCrdt), crdt.InstanceId);

            if (!_managedCrdts.TryAdd(typeAndInstanceId, crdt)
                || !_sameManagedCrdts.TryAdd(typeNameAndInstanceId, crdt))
            {
                throw new ManagedCrdtContextSetupException($"Failed to add crdt of type {typeof(TCrdt)} " +
                                                           $"with id {crdt.InstanceId} to the context - crdt of that" +
                                                           $" type and with that instanceId ({crdt.InstanceId}) was " +
                                                           "already added. Make sure instanceId " +
                                                           "is unique withing crdts of the same type.");
            }

            _crdtFactories.TryAdd(typeof(TCrdt), factory);

            _typeToTypeNameMapping.AddOrUpdate(typeof(TCrdt),
                _ => new HashSet<TypeNameAndInstanceId> {typeNameAndInstanceId},
                (_, nameAndId) =>
                {
                    nameAndId.Add(typeNameAndInstanceId);
                    return nameAndId;
                });

            if (crdt is ICreateAndDeleteManagedCrdtsInside compoundCrdt)
            {
                compoundCrdt.ManagedCrdtContext = this;
            }

            if (crdt is IRebalanceAtNodeChange crdtThatNeedsToRebalance)
            {
                Nodes.RebalanceOnChange(crdtThatNeedsToRebalance);
            }
        }

        internal void Add<TCrdt, TRepresentation, TDto>(TCrdt crdt,
            IManagedCRDTFactory<TCrdt, TRepresentation, TDto> factory,
            INodesWithReplicaProvider nodesWithReplicaProvider,
            ICreateAndDeleteManagedCrdtsInside holder)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            Add(crdt, factory);
            var nameAndInstanceId = new TypeNameAndInstanceId(TypeNameCompressor.GetName<TCrdt>(), crdt.InstanceId);
            _partiallyReplicated.TryAdd(nameAndInstanceId, nodesWithReplicaProvider);
            _holders.TryAdd(nameAndInstanceId, holder);
        }

        internal void Remove<TCrdt, TRepresentation, TDto>(TCrdt crdt)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            var typeNameAndInstanceId = new TypeNameAndInstanceId(TypeNameCompressor.GetName<TCrdt>(), crdt.InstanceId);

            _managedCrdts.TryRemove(new TypeAndInstanceId(typeof(TCrdt), crdt.InstanceId), out _);
            _sameManagedCrdts.TryRemove(typeNameAndInstanceId, out _);

            if(_typeToTypeNameMapping.TryGetValue(typeof(TCrdt), out var nameAndInstanceIds))
            {
                nameAndInstanceIds.Remove(typeNameAndInstanceId);
            }

            // Remove factory and mapping only if it's a last instances
            if (_managedCrdts.Keys.Any(key => key.Type == typeof(TCrdt))) return;

            _crdtFactories.TryRemove(typeof(TCrdt), out _);
            _typeToTypeNameMapping.TryRemove(typeof(TCrdt), out _);
        }

        internal async Task RemoveAsync<TCrdt, TRepresentation, TDto>(TypeNameAndInstanceId nameAndInstanceId,
            CancellationToken cancellationToken = default)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            if(_holders.TryGetValue(nameAndInstanceId, out var holder))
            {
                await holder.MarkForDeletionAsync(nameAndInstanceId.InstanceId, cancellationToken);
            }

            _partiallyReplicated.TryRemove(nameAndInstanceId, out _);
            if (TryGetCrdtWithFactory<TCrdt, TRepresentation, TDto>(nameAndInstanceId.InstanceId, out var crdt, out _))
            {
                Remove<TCrdt, TRepresentation, TDto>(crdt);
            }
        }

        public async Task<TDto> MergeAsync<TCrdt, TRepresentation, TDto>(TDto dto,
            string instanceId,
            int propagationCounter = 0,
            CancellationToken cancellationToken = default)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            if (!TryGetCrdtWithFactory<TCrdt, TRepresentation, TDto>(instanceId, out var crdt, out var factory))
            {
                throw new ManagedCrdtContextSetupException($"Merging dto of type {typeof(TDto)} with id {instanceId} " +
                                                           "failed. Check that you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            var other = factory.Create(dto, instanceId);
            var mergeResult = await crdt.MergeAsync(other, cancellationToken);
            if (mergeResult == MergeResult.ConflictSolved)
            {
                await crdt.StateChangedAsync(propagationCounter: propagationCounter, cancellationToken: cancellationToken);
            }
            return await crdt.ToDtoAsync(cancellationToken);
        }

        public async Task<TCollectionOperationResponse> ApplyAsync<TCrdt,
            TKey,
            TCollection,
            TCollectionRepresentation,
            TCollectionDto,
            TCollectionOperationBase,
            TCollectionOperationResponseBase,
            TCollectionFactory,
            TCollectionOperation,
            TCollectionOperationResponse
        >(
            TKey key,
            TCollectionOperation operation,
            string instanceId,
            CancellationToken cancellationToken = default)
            where TCrdt : PartiallyReplicatedCRDTRegistry<TCrdt,
                TKey,
                TCollection,
                TCollectionRepresentation,
                TCollectionDto,
                TCollectionOperationBase,
                TCollectionOperationResponseBase,
                TCollectionFactory>
            where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
            where TCollection : ManagedCRDTWithSerializableOperations<TCollection,
                TCollectionRepresentation,
                TCollectionDto,
                TCollectionOperationBase,
                TCollectionOperationResponseBase>
            where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionRepresentation, TCollectionDto>, new()
            where TCollectionOperationBase : Operation
            where TCollectionOperationResponseBase : OperationResponse
            where TCollectionOperation : TCollectionOperationBase
            where TCollectionOperationResponse : TCollectionOperationResponseBase
        {
            if (!TryGetCrdtWithFactory<TCrdt,
                    IReadOnlyDictionary<TKey, TCollectionRepresentation>,
                    PartiallyReplicatedCRDTRegistry<TCrdt,
                        TKey,
                        TCollection,
                        TCollectionRepresentation,
                        TCollectionDto,
                        TCollectionOperationBase,
                        TCollectionOperationResponseBase,
                        TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>(instanceId, out var crdt, out _))
            {
                throw new ManagedCrdtContextSetupException($"Applying operation of type {typeof(TCollectionOperationBase)} " +
                                                           $"to crdt {typeof(TCrdt)} with id {instanceId} failed. " +
                                                           "Check that you Add-ed appropriate partially replicated crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            return await crdt.ApplyAsync<TCollectionOperation, TCollectionOperationResponse>(key, operation, cancellationToken);
        }

        public ReadOnlySpan<byte> GetHash(TypeNameAndInstanceId nameAndInstanceId) =>
            _sameManagedCrdts.TryGetValue(nameAndInstanceId, out var crdt)
                ? crdt.CalculateHash()
                : ArraySegment<byte>.Empty;

        internal IEnumerable<string> GetInstanceIds<TCrdt>()
            => _managedCrdts.Keys.Where(tid => tid.Type == typeof(TCrdt)).Select(tid => tid.InstanceId);

        internal bool IsHashEqual(TypeNameAndInstanceId typeNameAndInstanceId, ReadOnlySpan<byte> hash)
        {
            if(!_sameManagedCrdts.TryGetValue(typeNameAndInstanceId, out var crdt))
            {
                throw new ManagedCrdtContextSetupException($"Checking hash for Crdt named {typeNameAndInstanceId.TypeName} " +
                                                           $"with id {typeNameAndInstanceId.InstanceId} failed - " +
                                                           "Crdt not found in the managed context. Check that you " +
                                                           "Add-ed appropriate managed crdt type and that instanceId " +
                                                           "of that type is coordinated across servers");
            }

            return crdt.CalculateHash().SequenceEqual(hash);
        }

        public async IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync<TCrdt, TRepresentation, TDto>(string instanceId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            if (!_managedCrdts.TryGetValue(new TypeAndInstanceId(typeof(TCrdt), instanceId), out var crdtObject))
            {
                throw new ManagedCrdtContextSetupException($"Enumerating dto of crdt type {typeof(TCrdt)} " +
                                                           $"with id {instanceId} failed due to crdt not being found." +
                                                           " Did you forget to add that Crdt in ManagedContext?");
            }

            if (crdtObject is not IAsyncDtoBatchProvider<TDto> batchProvider)
            {
                throw new ManagedCrdtContextSetupException($"Internal error: Crdt of type {crdtObject.GetType()} does " +
                                                           "not implement IAsyncDtoBatchProvider. This should not happen.");
            }

            await foreach (var dto in batchProvider.EnumerateDtoBatchesAsync(cancellationToken))
            {
                yield return dto;
            }
        }

        internal IEnumerable<NodeInfo> GetNodesThatHaveReplica(TypeNameAndInstanceId nameAndInstanceId)
        {
            if (_partiallyReplicated.TryGetValue(nameAndInstanceId, out var nodesWithReplicasProvider))
            {
                return nodesWithReplicasProvider.GetNodesThatShouldHaveReplicaOfCollection(nameAndInstanceId.InstanceId);
            }

            return Nodes.Value;
        }

        // ReSharper disable once MemberCanBePrivate.Global - used in a generated code
        public bool TryGetCrdtWithFactory<TCrdt, TRepresentation, TDto>(string instanceId,
            [NotNullWhen(true)] out TCrdt? crdt,
            [NotNullWhen(true)] out IManagedCRDTFactory<TCrdt, TRepresentation, TDto>? factory)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
        {
            _managedCrdts.TryGetValue(new TypeAndInstanceId(typeof(TCrdt), instanceId), out var crdtObject);
            _crdtFactories.TryGetValue(typeof(TCrdt), out var factoryObject);

            crdt = crdtObject as TCrdt;
            factory = factoryObject as IManagedCRDTFactory<TCrdt, TRepresentation, TDto>;
            return crdt != null && factory != null;
        }
    }
}
