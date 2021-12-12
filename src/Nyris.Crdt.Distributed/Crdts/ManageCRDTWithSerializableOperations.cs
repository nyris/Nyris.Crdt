using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts
{
    public abstract class ManageCRDTWithSerializableOperations<TImplementation, TRepresentation, TDto, TOperation>
        : ManagedCRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
    {
        /// <inheritdoc />
        protected ManageCRDTWithSerializableOperations(string instanceId) : base(instanceId)
        {
        }

        public abstract Task ApplyAsync<T>(T operation) where T : TOperation;
    }
}