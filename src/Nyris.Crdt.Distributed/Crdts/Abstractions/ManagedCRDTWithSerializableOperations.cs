using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract class ManagedCRDTWithSerializableOperations<TImplementation, TRepresentation, TDto, TOperation, TOperationResponse>
        : ManagedCRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
        where TOperation : Operation
        where TOperationResponse : OperationResponse
    {
        public abstract ulong Size { get; }

        /// <inheritdoc />
        protected ManagedCRDTWithSerializableOperations(string instanceId) : base(instanceId)
        {
        }

        public abstract Task<TOperationResponse> ApplyAsync(TOperation operation);
    }
}