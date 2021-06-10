using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Exceptions;

namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCrdtContext
    {
        private readonly Dictionary<(Type dtoType, int id), object> _managedCrdts = new();
        private readonly Dictionary<Type, object> _crdtFactories = new();

        protected void Add<T, T1, T2, T3>(T crdt, ICRDTFactory<T1, T2, T3> factory)
            where T : ManagedCRDT<T1, T2, T3>
            where T1 : ICRDT<T1, T2, T3>
        {
            if (!_managedCrdts.TryAdd((crdt.GetType(), crdt.InstanceId), crdt))
            {
                throw new ManagedCrdtContextSetupException($"Failed to add crdt of type {crdt.GetType()} to " +
                                                           $"the context - crdt of that type and with that instanceId " +
                                                           $"({crdt.InstanceId}) was already added. Make sure instanceId " +
                                                           $"is unique withing crdts of the same type.");
            }
            _crdtFactories.TryAdd(typeof(T1), factory);
        }

        public TDto Merge<TCrdt, T2, TDto>(WithId<TDto> dto)
            where TCrdt : ManagedCRDT<TCrdt, T2, TDto>
        {
            if (!_managedCrdts.TryGetValue((typeof(TCrdt), dto.Id), out var crdtObject)
                || crdtObject is not TCrdt crdt
                || !_crdtFactories.TryGetValue(typeof(TCrdt), out var factoryObject)
                || factoryObject is not ICRDTFactory<TCrdt, T2, TDto> factory)
            {
                throw new ManagedCrdtContextSetupException($"Merging dto of type {typeof(TDto)} failed. " +
                                                           "Check that you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinate across servers");
            }

            var other = factory.Create(dto.Dto);
            crdt.Merge(other);
            return crdt.ToDto();
        }
    }
}