using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    /// <summary>
    /// Base type for all managedCRDTs. When inheriting, do not forget to call <see cref="StateChangedAsync"/> in all update methods.
    /// </summary>
    /// <remarks>
    ///     DO NOT CHANGE THIS TYPE'S NAME LIGHTLY
    ///     It is used without referencing the type directly (or project at all) in the SourceGenerator project as const string.
    /// </remarks>
    /// <typeparam name="TImplementation">The type that implements <see cref="IAsyncCRDT{TImplementation, TRepresentation, TDto}"/>.
    /// Usually you pass here the your type, that inherits ManagedCRDT. </typeparam>
    /// <typeparam name="TRepresentation">That's how CRDT looks on the outside.
    /// For example, a fancy CRDT set, which tracks who is doing the deletion, still provides
    /// a basic HashSet<> Value to it's users.</typeparam>
    /// <typeparam name="TDto">It's a dto type, that is used as a data contract for grpc
    /// communication. So it should be properly annotated with <see cref="ProtoContract"/>
    /// and <see cref="ProtoMember"/>.</typeparam>
    public abstract class ManagedCRDT<TImplementation, TRepresentation, TDto> : IAsyncCRDT<TImplementation, TRepresentation, TDto>, IHashable
        where TImplementation : ManagedCRDT<TImplementation, TRepresentation, TDto>
    {
        public readonly string InstanceId;
        private readonly AsyncQueue<DtoMessage<TDto>> _queue;
        private string? _typeName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="instanceId">Instance id is used to match instances of the same type across nodes.
        /// For example, if there nodes (servers) A and B and they try to share two instanced of CRDT type T.
        /// When node A updates first instance and sends that update to B, B needs to somehow
        /// distinguish which instance was updated. </param>
        protected ManagedCRDT(string instanceId)
        {
            _queue = Queues.GetQueue<TDto>(GetType());
            InstanceId = instanceId;
        }

        protected string TypeName
        {
            get
            {
                if (_typeName != null) return _typeName;
                _typeName = TypeNameCompressor.GetName(GetType());
                return _typeName;
            }
        }

        /// <inheritdoc />
        public abstract TRepresentation Value { get; }

        /// <inheritdoc />
        public abstract Task<MergeResult> MergeAsync(TImplementation other, CancellationToken cancellationToken = default);

        /// <param name="cancellationToken"></param>
        /// <inheritdoc />
        public abstract Task<TDto> ToDtoAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default);

        protected internal async Task StateChangedAsync(int propagationCounter = 0, CancellationToken cancellationToken = default)
        {
            var dtoMessage = new DtoMessage<TDto>(TypeName,
                InstanceId,
                await ToDtoAsync(cancellationToken),
                propagationCounter);

            _queue.Enqueue(dtoMessage);
            await dtoMessage.MaybeWaitForCompletionAsync(cancellationToken);
        }

        /// <inheritdoc />
        public abstract ReadOnlySpan<byte> CalculateHash();
    }
}