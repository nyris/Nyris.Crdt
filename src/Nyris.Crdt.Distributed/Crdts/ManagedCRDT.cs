using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts
{
    /// <summary>
    /// Base type for all managedCRDTs. When inheriting, do not forget to call <see cref="StateChanged"/> in all update methods.
    /// </summary>
    /// <remarks>
    ///     DO NOT CHANGE THIS TYPE'S NAME LIGHTLY
    ///     It is used without referencing the type directly (or project at all) in the SourceGenerator project as const string.
    /// </remarks>
    /// <typeparam name="TImplementation"></typeparam>
    /// <typeparam name="TRepresentation"></typeparam>
    /// <typeparam name="TDto"></typeparam>
    public abstract class ManagedCRDT<TImplementation, TRepresentation, TDto> : ICRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
    {
        public readonly int InstanceId;
        private readonly AsyncQueue<WithId<TDto>> _queue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="instanceId">Instance id is used to match instances of the same type across nodes.
        /// For example, if there nodes (servers) A and B and they try to share two instanced of CRDT type T.
        /// When node A updates first instance and sends that update to B, B needs to somehow
        /// distinguish which instance was updated. </param>
        protected ManagedCRDT(int instanceId)
        {
            _queue = Queues.GetQueue<TDto>(GetType());
            InstanceId = instanceId;
        }

        // private AsyncQueue<WithId<TDto>> Queue => _queue ??= Queues.GetQueue<WithId<TDto>>();

        /// <inheritdoc />
        public abstract TRepresentation Value { get; }

        /// <inheritdoc />
        public abstract MergeResult Merge(TImplementation other);

        /// <inheritdoc />
        public abstract TDto ToDto();

        protected void StateChanged() => _queue.Enqueue(ToDto().WithId(InstanceId));
    }
}