using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageInfoLwwCollectionWithSerializableOperations
        : ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<ImageInfoLwwCollectionWithSerializableOperations,
            ImageGuid,
            ImageInfo,
            DateTime>
    {
        /// <inheritdoc />
        public ImageInfoLwwCollectionWithSerializableOperations(string instanceId) : base(instanceId)
        {
        }

        /// <inheritdoc />
        public ImageInfoLwwCollectionWithSerializableOperations(LWWDto dto, string instanceId) : base(dto, instanceId)
        {
        }

        public static ImageInfoLwwCollectionWithSerializableOperationsFactory DefaultFactory = new();

        public sealed class ImageInfoLwwCollectionWithSerializableOperationsFactory
            : IManagedCRDTFactory<ImageInfoLwwCollectionWithSerializableOperations,
                IReadOnlyDictionary<ImageGuid, ImageInfo>,
                LWWDto>
        {
            /// <inheritdoc />
            public ImageInfoLwwCollectionWithSerializableOperations Create(LWWDto dto,
                string instanceId) => new(dto, instanceId);

            /// <inheritdoc />
            public ImageInfoLwwCollectionWithSerializableOperations Create(string instanceId)
                => new (instanceId);
        }
    }
}