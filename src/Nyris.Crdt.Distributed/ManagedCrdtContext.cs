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
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCrdtContext
    {
        private readonly ConcurrentDictionary<TypeAndInstanceId, object> _managedCrdts = new();
        private readonly ConcurrentDictionary<TypeAndInstanceId, INodesWithReplicaProvider> _partiallyReplicated = new();
        private readonly ConcurrentDictionary<Type, object> _crdtFactories = new();
        private readonly ConcurrentDictionary<TypeNameAndInstanceId, IHashable> _sameManagedCrdts = new();
        private readonly ConcurrentDictionary<Type, HashSet<TypeNameAndInstanceId>> _typeToTypeNameMapping = new();

        internal readonly NodeSet Nodes = new ("nodes_internal");

        protected ManagedCrdtContext()
        {
            Add(Nodes, NodeSet.DefaultFactory);
        }

        protected internal void Add<TCrdt, TImplementation, TRepresentation, TDto>(TCrdt crdt, IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto> factory)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
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

            if (crdt is ICreateManagedCrdtsInside compoundCrdt)
            {
                compoundCrdt.ManagedCrdtContext = this;
            }

            if (crdt is IRebalanceAtNodeChange crdtThatNeedsToRebalance)
            {
                Nodes.RebalanceOnChange(crdtThatNeedsToRebalance);
            }
        }

        internal void Add<TCrdt, TImplementation, TRepresentation, TDto>(TCrdt crdt,
            IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto> factory,
            INodesWithReplicaProvider nodesWithReplicaProvider)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
        {
            Add(crdt, factory);
            _partiallyReplicated.TryAdd(new TypeAndInstanceId(typeof(TCrdt), crdt.InstanceId), nodesWithReplicaProvider);
        }

        internal void Remove<TCrdt, TImplementation, TRepresentation, TDto>(TCrdt crdt)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
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

        public async Task<TDto> MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(TDto dto, string instanceId,
            CancellationToken cancellationToken = default)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
        {
            if (!TryGetCrdtWithFactory<TCrdt, TImplementation, TRepresentation, TDto>(instanceId, out var crdt, out var factory))
            {
                throw new ManagedCrdtContextSetupException($"Merging dto of type {typeof(TDto)} with id {instanceId} " +
                                                           "failed. Check that you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            var other = factory.Create(dto, instanceId);
            await crdt.MergeAsync(other, cancellationToken);
            return await crdt.ToDtoAsync(cancellationToken);
        }

        public async Task ApplyAsync<TCrdt, TKey, TCollection, TCollectionRepresentation, TCollectionDto, TCollectionOperation, TCollectionFactory>(TKey key,
            TCollectionOperation operation,
            string instanceId,
            CancellationToken cancellationToken = default)
            where TCrdt : PartiallyReplicatedCRDTRegistry<TCrdt, TKey, TCollection, TCollectionRepresentation, TCollectionDto, TCollectionOperation, TCollectionFactory>
            where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
            where TCollection : ManagedCRDTWithSerializableOperations<TCollection, TCollectionRepresentation, TCollectionDto, TCollectionOperation>
            where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionRepresentation, TCollectionDto>, new()
        {
            if (!TryGetCrdtWithFactory<TCrdt,
                    TCrdt,
                    Dictionary<TKey, TCollection>,
                    PartiallyReplicatedCRDTRegistry<TCrdt,
                        TKey,
                        TCollection,
                        TCollectionRepresentation,
                        TCollectionDto,
                        TCollectionOperation,
                        TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>(instanceId, out var crdt, out _))
            {
                throw new ManagedCrdtContextSetupException($"Applying operation of type {typeof(TCollectionOperation)} " +
                                                           $"to crdt {typeof(TCrdt)} with id {instanceId} failed. " +
                                                           "Check that you Add-ed appropriate partially replicated crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            await crdt.ApplyAsync(key, operation, cancellationToken);
        }

        public ReadOnlySpan<byte> GetHash(TypeNameAndInstanceId nameAndInstanceId) =>
            _sameManagedCrdts.TryGetValue(nameAndInstanceId, out var crdt)
                ? crdt.CalculateHash()
                : ArraySegment<byte>.Empty;

        internal IEnumerable<string> GetInstanceIds<TCrdt>()
            => _managedCrdts.Keys.Where(tid => tid.Type == typeof(TCrdt)).Select(tid => tid.InstanceId);

        internal bool IsHashEqual(WithId<TypeNameAndHash> hash)
        {
            if(!_sameManagedCrdts.TryGetValue(new TypeNameAndInstanceId(hash.Value!.TypeName, hash.Id), out var crdt))
            {
                throw new ManagedCrdtContextSetupException($"Checking hash for Crdt named {hash.Value.TypeName} " +
                                                           $"with id {hash.Id} failed - Crdt not found in the managed" +
                                                           " context. Check that you Add-ed appropriate managed crdt " +
                                                           "type and that instanceId of that type is coordinated " +
                                                           "across servers");
            }

            return crdt.CalculateHash().SequenceEqual(new ReadOnlySpan<byte>(hash.Value.Hash));
        }

        public async IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync<TCrdt, TImplementation, TRepresentation, TDto>(string instanceId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
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

        internal IEnumerable<NodeInfo> GetNodesThatHaveReplica<TCrdt>(string instanceId)
        {
            if (_partiallyReplicated.TryGetValue(new TypeAndInstanceId(typeof(TCrdt), instanceId), out var nodesWithReplicasProvider))
            {
                return nodesWithReplicasProvider.GetNodesThatShouldHaveReplicaOfCollection(instanceId);
            }

            return Nodes.Value;
        }

        // ReSharper disable once MemberCanBePrivate.Global - used in a generated code
        public bool TryGetCrdtWithFactory<TCrdt, TImplementation, TRepresentation, TDto>(string instanceId,
            [NotNullWhen(true)] out TCrdt? crdt,
            [NotNullWhen(true)] out IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto>? factory)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>
            where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
        {
            _managedCrdts.TryGetValue(new TypeAndInstanceId(typeof(TCrdt), instanceId), out var crdtObject);
            _crdtFactories.TryGetValue(typeof(TCrdt), out var factoryObject);

            crdt = crdtObject as TCrdt;
            factory = factoryObject as IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto>;
            return crdt != null && factory != null;
        }
    }
}
