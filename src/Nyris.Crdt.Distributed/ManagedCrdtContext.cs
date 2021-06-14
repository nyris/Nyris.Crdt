using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        private readonly ConcurrentDictionary<Type, HashSet<TypeNameAndInstanceId>> _typeToTypeNameMapping = new();

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

            var typeNameAndInstanceId = new TypeNameAndInstanceId(crdt.TypeName, crdt.InstanceId);

            if (!_managedCrdts.TryAdd(new TypeAndInstanceId(typeof(TCrdt), crdt.InstanceId), crdt)
                || !_sameManagedCrdts.TryAdd(typeNameAndInstanceId, crdt))
            {
                throw new ManagedCrdtContextSetupException($"Failed to add crdt of type {typeof(TCrdt)} to " +
                                                           "the context - crdt of that type and with that instanceId " +
                                                           $"({crdt.InstanceId}) was already added. Make sure instanceId " +
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
            var typeNameAndInstanceId = new TypeNameAndInstanceId(crdt.TypeName, crdt.InstanceId);

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

        /// <summary>
        /// Gets hashes of all instances of crdt of a given type
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<HashAndInstanceId> GetHashesAsync(string typeName)
        {
            foreach (var typeNameAndId in _sameManagedCrdts.Keys.Where(key => key.TypeName == typeName))
            {
                if (_sameManagedCrdts.TryGetValue(typeNameAndId, out var crdt))
                {
                    yield return new HashAndInstanceId(Hash: await crdt.GetHashAsync(), InstanceId: typeNameAndId.InstanceId);
                }
            }
        }

        public string GetTypeName<TCrdt>()
        {
            if (!_typeToTypeNameMapping.TryGetValue(typeof(TCrdt), out var set) || set.Count == 0)
            {
                throw new ManagedCrdtContextSetupException($"Getting type name for {typeof(TCrdt)} has failed - " +
                                                           "did you forget to add that Crdt in ManagedContext?");
            }

            return set.First().TypeName;
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

        public async IAsyncEnumerable<WithId<TDto>> EnumerateDtoBatchesAsync<TCrdt, TImplementation, TRepresentation, TDto>(string instanceId)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
        {
            if (!_managedCrdts.TryGetValue(new TypeAndInstanceId(typeof(TCrdt), instanceId), out var crdtObject))
            {
                throw new ManagedCrdtContextSetupException($"Enumerating dto of crdt type {typeof(TCrdt)} failed" +
                                                           " due to crdt not being found. " +
                                                           "Did you forget to add that Crdt in ManagedContext?");
            }

            if (crdtObject is not IAsyncDtoBatchProvider<TDto> batchProvider)
            {
                throw new ManagedCrdtContextSetupException($"Internal error: Crdt of type {crdtObject.GetType()} does " +
                                                           "not implement IAsyncDtoBatchProvider. This should not happen.");
            }

            await foreach (var dto in batchProvider.EnumerateDtoBatchesAsync())
            {
                yield return dto.WithId(instanceId);
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
