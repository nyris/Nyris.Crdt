using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;

namespace Nyris.Crdt.GrpcServiceSample
{
    public sealed class ImageInfoLwwRegistry : ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>
    {
        /// <inheritdoc />
        public ImageInfoLwwRegistry(string id) : base(id)
        {
        }

        /// <inheritdoc />
        private ImageInfoLwwRegistry(LastWriteWinsDto dto, string instanceId) : base(dto, instanceId)
        {
        }

        public static IManagedCRDTFactory<ImageInfoLwwRegistry,
            ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
            IReadOnlyDictionary<Guid, ImageInfo>,
            LastWriteWinsDto> DefaultFactory = new ItemInfoLwwRegistryFactory();

        public sealed class ItemInfoLwwRegistryFactory : IManagedCRDTFactory<ImageInfoLwwRegistry,
            ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
            IReadOnlyDictionary<Guid, ImageInfo>,
            LastWriteWinsDto>
        {
            /// <inheritdoc />
            public ImageInfoLwwRegistry Create(LastWriteWinsDto dto, string instanceId) => new(dto, instanceId);

            /// <inheritdoc />
            public ImageInfoLwwRegistry Create(string instanceId) => new ImageInfoLwwRegistry(instanceId);
        }
    }
}
