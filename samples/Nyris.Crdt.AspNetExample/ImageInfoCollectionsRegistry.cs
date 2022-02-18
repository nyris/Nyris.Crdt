using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageInfoCollectionsRegistry : ManagedCrdtRegistry<NodeId,
        CollectionId,
        ImageInfoLwwCollection,
        ImageInfoLwwCollection.LastWriteWinsDto,
        ImageInfoLwwCollection.ImageInfoLwwCollectionFactory>
    {
        /// <inheritdoc />
        public ImageInfoCollectionsRegistry(string id, ILogger? logger = null) : base(id, logger: logger)
        {
        }

        public static readonly RegistryFactory DefaultFactory = new RegistryFactory();

        public sealed class RegistryFactory : IManagedCRDTFactory<ImageInfoCollectionsRegistry,
            RegistryDto>
        {
            /// <inheritdoc />
            public ImageInfoCollectionsRegistry Create(string instanceId) => new(instanceId);
        }
    }
}