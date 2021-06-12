using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCrdtContext
    {
        private readonly Dictionary<(Type dtoType, int id), object> _managedCrdts = new();
        private readonly Dictionary<Type, object> _crdtFactories = new();

        internal readonly NodeSet Nodes = new (-1);

        protected ManagedCrdtContext()
        {
            Add(Nodes, NodeSet.DefaultFactory);
        }

        protected void Add<TCrdt, TImplementation, TRepresentation, TDto>(TCrdt crdt, ICRDTFactory<TCrdt, TImplementation, TRepresentation, TDto> factory)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
        {
            if (typeof(TCrdt).IsGenericType)
            {
                throw new ManagedCrdtContextSetupException("Only non generic types can be used in a managed " +
                                                           "context (because library needs to generate concrete grpc " +
                                                           "messaged based on dto type used)." +
                                                           $"Try creating concrete type, that inherits from {typeof(TCrdt)}");
            }

            if (!_managedCrdts.TryAdd((typeof(TCrdt), crdt.InstanceId), crdt))
            {
                throw new ManagedCrdtContextSetupException($"Failed to add crdt of type {typeof(TCrdt)} to " +
                                                           "the context - crdt of that type and with that instanceId " +
                                                           $"({crdt.InstanceId}) was already added. Make sure instanceId " +
                                                           "is unique withing crdts of the same type.");
            }

            _crdtFactories.TryAdd(typeof(TCrdt), factory);
        }

        public TDto Merge<TCrdt, TImplementation, TRepresentation, TDto>(WithId<TDto> dto)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
        {
            if (!TryGetCrdtWithFactory<TCrdt, TImplementation, TRepresentation, TDto>(dto.Id, out var crdt, out var factory))
            {
                throw new ManagedCrdtContextSetupException($"Merging dto of type {typeof(TDto)} failed. " +
                                                           "Check that you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            var other = factory.Create(dto.Dto);
            crdt.Merge(other);
            return crdt.ToDto();
        }

        private bool TryGetCrdtWithFactory<TCrdt, TImplementation, TRepresentation, TDto>(int instanceId,
            [NotNullWhen(true)] out TCrdt? crdt,
            [NotNullWhen(true)] out ICRDTFactory<TCrdt, TImplementation, TRepresentation, TDto>? factory)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>
            where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
        {
            _managedCrdts.TryGetValue((typeof(TCrdt), instanceId), out var crdtObject);
            _crdtFactories.TryGetValue(typeof(TCrdt), out var factoryObject);

            crdt = crdtObject as TCrdt;
            factory = factoryObject as ICRDTFactory<TCrdt, TImplementation, TRepresentation, TDto>;
            return crdt != null && factory != null;
        }
    }
}
