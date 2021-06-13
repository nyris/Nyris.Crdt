using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCrdtContext
    {
        private readonly ConcurrentDictionary<TypeAndInstanceId, object> _managedCrdts = new();
        private readonly ConcurrentDictionary<Type, object> _crdtFactories = new();
        private readonly ConcurrentDictionary<TypeNameAndInstanceId, IHashableAndHaveUniqueName> _sameManagedCrdts = new();

        internal readonly NodeSet Nodes = new ("");

        protected ManagedCrdtContext()
        {
            Add(Nodes, NodeSet.DefaultFactory);
        }

        protected internal void Add<TCrdt, TImplementation, TRepresentation, TDto>(TCrdt crdt, IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto> factory)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        {
            if (typeof(TCrdt).IsGenericType)
            {
                throw new ManagedCrdtContextSetupException("Only non generic types can be used in a managed " +
                                                           "context (because library needs to generate concrete grpc " +
                                                           "messaged based on dto type used)." +
                                                           $"Try creating concrete type, that inherits from {typeof(TCrdt)}");
            }

            if (!_managedCrdts.TryAdd(new TypeAndInstanceId(typeof(TCrdt), crdt.InstanceId), crdt)
                || !_sameManagedCrdts.TryAdd(new TypeNameAndInstanceId(crdt.TypeName, crdt.InstanceId), crdt))
            {
                throw new ManagedCrdtContextSetupException($"Failed to add crdt of type {typeof(TCrdt)} to " +
                                                           "the context - crdt of that type and with that instanceId " +
                                                           $"({crdt.InstanceId}) was already added. Make sure instanceId " +
                                                           "is unique withing crdts of the same type.");
            }

            _crdtFactories.TryAdd(typeof(TCrdt), factory);

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (crdt is ICreateManagedCrdtsInside compoundCrdt)
            {
                compoundCrdt.ManagedCrdtContext = this;
            }
        }

        internal void Remove<TCrdt, TImplementation, TRepresentation, TDto>(TCrdt crdt)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        {
            _managedCrdts.TryRemove(new TypeAndInstanceId(typeof(TCrdt), crdt.InstanceId), out _);
            _sameManagedCrdts.TryRemove(new TypeNameAndInstanceId(crdt.TypeName, crdt.InstanceId), out _);
            _crdtFactories.TryRemove(typeof(TCrdt), out _);
        }

        public async Task<TDto> MergeAsync<TCrdt, TImplementation, TRepresentation, TDto>(WithId<TDto> dto)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        {
            if (!TryGetCrdtWithFactory<TCrdt, TImplementation, TRepresentation, TDto>(dto.Id, out var crdt, out var factory))
            {
                throw new ManagedCrdtContextSetupException($"Merging dto of type {typeof(TDto)} failed. " +
                                                           "Check that you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            var other = factory.Create(dto.Dto);
            await crdt.MergeAsync(other);
            return await crdt.ToDtoAsync();
        }

        public async IAsyncEnumerable<WithId<TypeNameAndHash>> GetHashesAsync()
        {
            foreach (var typeNameAndId in _sameManagedCrdts.Keys)
            {
                if (_sameManagedCrdts.TryGetValue(typeNameAndId, out var crdt))
                {
                    yield return new TypeNameAndHash(typeNameAndId.TypeName, await crdt.GetHashAsync())
                        .WithId(typeNameAndId.InstanceId);
                }
            }
        }

        public async Task<bool> IsHashEqual(WithId<TypeNameAndHash> hash)
        {
            if(!_sameManagedCrdts.TryGetValue(new TypeNameAndInstanceId(hash.Dto.TypeName, hash.Id), out var crdt))
            {
                throw new ManagedCrdtContextSetupException($"Checking hash for Crdt named {hash.Dto.TypeName} " +
                                                           "failed - Crdt not found in the managed context. Check that " +
                                                           "you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            return string.Equals(await crdt.GetHashAsync(), hash.Dto.Hash, StringComparison.Ordinal);
        }

        public async IAsyncEnumerable<WithId<TDto>> EnumerateDtoBatchesAsync<TDto>(TypeNameAndInstanceId nameAndInstanceId)
        {
            if(!_sameManagedCrdts.TryGetValue(nameAndInstanceId, out var crdt))
            {
                throw new ManagedCrdtContextSetupException($"Checking hash for Crdt named {nameAndInstanceId.TypeName} " +
                                                           "failed - Crdt not found in the managed context. Check that " +
                                                           "you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            if (crdt is not IAsyncDtoBatchProvider<TDto> batchProvider)
            {
                throw new ManagedCrdtContextSetupException($"Internal error: Crdt of type {crdt.GetType()} does " +
                                                           "not implement IAsyncDtoBatchProvider. This should not happen.");
            }

            await foreach (var dto in batchProvider.EnumerateDtoBatchesAsync())
            {
                yield return dto.WithId(nameAndInstanceId.InstanceId);
            }
        }

        private bool TryGetCrdtWithFactory<TCrdt, TImplementation, TRepresentation, TDto>(string instanceId,
            [NotNullWhen(true)] out TCrdt? crdt,
            [NotNullWhen(true)] out IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto>? factory)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>
            where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        {
            _managedCrdts.TryGetValue(new TypeAndInstanceId(typeof(TCrdt), instanceId), out var crdtObject);
            _crdtFactories.TryGetValue(typeof(TCrdt), out var factoryObject);

            crdt = crdtObject as TCrdt;
            factory = factoryObject as IManagedCRDTFactory<TCrdt, TImplementation, TRepresentation, TDto>;
            return crdt != null && factory != null;
        }
    }
}
