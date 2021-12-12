using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts;

namespace Nyris.Crdt.AspNetExample
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
            Dictionary<Guid, ImageInfo>, LastWriteWinsDto> DefaultFactory = new ItemInfoLwwRegistryFactory();

        public sealed class ItemInfoLwwRegistryFactory : IManagedCRDTFactory<ImageInfoLwwRegistry,
            ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
            Dictionary<Guid, ImageInfo>,
            LastWriteWinsDto>
        {
            /// <inheritdoc />
            public ImageInfoLwwRegistry Create(LastWriteWinsDto dto, string instanceId) => new(dto, instanceId);
        }

        // /// <inheritdoc />
        // public override string TypeName => nameof(ImageInfoLwwRegistry);
    }
}
