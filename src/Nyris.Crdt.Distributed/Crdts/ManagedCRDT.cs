using System.Collections.Generic;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;

namespace Nyris.Crdt.Distributed.Crdts
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
    public abstract class ManagedCRDT<TImplementation, TRepresentation, TDto> : IAsyncCRDT<TImplementation, TRepresentation, TDto>, IHashableAndHaveUniqueName
        where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
    {
        public readonly string InstanceId;
        private readonly AsyncQueue<WithId<TDto>> _queue;

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

        // private AsyncQueue<WithId<TDto>> Queue => _queue ??= Queues.GetQueue<WithId<TDto>>();

        /// <inheritdoc />
        public abstract TRepresentation Value { get; }

        /// <inheritdoc />
        public abstract Task<MergeResult> MergeAsync(TImplementation other);

        /// <inheritdoc />
        public abstract Task<TDto> ToDtoAsync();

        /// <inheritdoc />
        public abstract IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync();

        protected async Task StateChangedAsync() => _queue.Enqueue((await ToDtoAsync()).WithId(InstanceId));

        /// <inheritdoc />
        public abstract string TypeName { get; }

        /// <inheritdoc />
        public abstract Task<string> GetHashAsync();
    }
}