using System;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageInfoLwwCollection : ManagedLastWriteWinsDeltaRegistry<ImageGuid, ImageInfo, DateTime>
    {
        /// <inheritdoc />
        public ImageInfoLwwCollection(string id) : base(id)
        {
        }


        public sealed class ImageInfoLwwCollectionFactory : IManagedCRDTFactory<ImageInfoLwwCollection,
            LastWriteWinsDto>
        {
            /// <inheritdoc />
            public ImageInfoLwwCollection Create(string instanceId) => new ImageInfoLwwCollection(instanceId);
        }
    }
}
