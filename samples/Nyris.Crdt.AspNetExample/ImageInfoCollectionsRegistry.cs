using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageInfoCollectionsRegistry : ManagedCrdtRegistry<ImageInfoCollectionsRegistry,
        NodeId,
        CollectionId,
        ImageInfoLwwCollection,
        IReadOnlyDictionary<ImageGuid, ImageInfo>,
        ImageInfoLwwCollection.LastWriteWinsDto,
        ImageInfoLwwCollection.ImageInfoLwwCollectionFactory>
    {
        /// <inheritdoc />
        public ImageInfoCollectionsRegistry(string id) : base(id)
        {
        }

        private ImageInfoCollectionsRegistry(RegistryDto registryDto, string instanceId) : base(registryDto, instanceId)
        {
        }

        public static readonly RegistryFactory DefaultFactory = new RegistryFactory();

        public sealed class RegistryFactory : IManagedCRDTFactory<ImageInfoCollectionsRegistry,
            IReadOnlyDictionary<CollectionId, IReadOnlyDictionary<ImageGuid, ImageInfo>>,
            RegistryDto>
        {
            /// <inheritdoc />
            public ImageInfoCollectionsRegistry Create(RegistryDto registryDto, string instanceId) => new(registryDto, instanceId);

            /// <inheritdoc />
            public ImageInfoCollectionsRegistry Create(string instanceId) => new(instanceId);
        }
    }
}