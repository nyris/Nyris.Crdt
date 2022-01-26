using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageInfoLwwCollection : ManagedLastWriteWinsDeltaRegistry<ImageInfoLwwCollection, ImageGuid, ImageInfo, DateTime>
    {
        /// <inheritdoc />
        public ImageInfoLwwCollection(string id) : base(id)
        {
        }

        /// <inheritdoc />
        private ImageInfoLwwCollection(LastWriteWinsDto dto, string instanceId) : base(dto, instanceId)
        {
        }

        public sealed class ImageInfoLwwCollectionFactory : IManagedCRDTFactory<ImageInfoLwwCollection,
            IReadOnlyDictionary<ImageGuid, ImageInfo>,
            LastWriteWinsDto>
        {
            /// <inheritdoc />
            public ImageInfoLwwCollection Create(LastWriteWinsDto dto, string instanceId) => new(dto, instanceId);

            /// <inheritdoc />
            public ImageInfoLwwCollection Create(string instanceId) => new ImageInfoLwwCollection(instanceId);
        }
    }
}
