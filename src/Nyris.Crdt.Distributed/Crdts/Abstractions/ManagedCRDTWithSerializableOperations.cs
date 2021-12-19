using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract class ManagedCRDTWithSerializableOperations<TImplementation, TRepresentation, TDto, TOperation>
        : ManagedCRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
    {
        /// <inheritdoc />
        protected ManagedCRDTWithSerializableOperations(string instanceId) : base(instanceId)
        {
        }

        public abstract Task ApplyAsync<T>(T operation) where T : TOperation;
    }
}