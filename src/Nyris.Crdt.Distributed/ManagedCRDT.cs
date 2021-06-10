namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCRDT<TImplementation, TRepresentation, TDto> : ICRDT<TImplementation, TRepresentation, TDto>
        where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
    {
        internal readonly int InstanceId;
        private AsyncQueue<WithId<TDto>>? _queue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="instanceId">Instance id is used to match instances of the same type across nodes.
        /// For example, if there nodes (servers) A and B and they try to share two instanced of CRDT type T.
        /// When node A updates first instance and sends that update to B, B needs to somehow
        /// distinguish which instance was updated. </param>
        protected ManagedCRDT(int instanceId)
        {
            InstanceId = instanceId;
        }

        // private AsyncQueue<WithId<TDto>> Queue => _queue ??= Queues.GetQueue<WithId<TDto>>();

        /// <inheritdoc />
        public abstract TRepresentation Value { get; }

        /// <inheritdoc />
        public abstract MergeResult Merge(TImplementation other);

        /// <inheritdoc />
        public abstract TDto ToDto();

        protected void StateChanged()
        {
            // get queue by type and instance id
        }
    }
}