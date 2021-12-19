using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.GrpcServiceSample
{
    public sealed class ItemInfoCollectionsRegistry : ManagedCrdtRegistry<NodeId,
        IndexId,
        ImageInfoLwwRegistry,
        ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
        IReadOnlyDictionary<Guid, ImageInfo>,
        ImageInfoLwwRegistry.LastWriteWinsDto,
        ImageInfoLwwRegistry.ItemInfoLwwRegistryFactory>
    {
        /// <inheritdoc />
        public ItemInfoCollectionsRegistry(string id) : base(id)
        {
        }

        private ItemInfoCollectionsRegistry(RegistryDto registryDto, string instanceId) : base(registryDto, instanceId)
        {
        }

        public static readonly IManagedCRDTFactory<ItemInfoCollectionsRegistry,
                ManagedCrdtRegistry<NodeId,
                    IndexId,
                    ImageInfoLwwRegistry,
                    ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
                    IReadOnlyDictionary<Guid, ImageInfo>,
                    ImageInfoLwwRegistry.LastWriteWinsDto,
                    ImageInfoLwwRegistry.ItemInfoLwwRegistryFactory>,
                IReadOnlyDictionary<IndexId, IReadOnlyDictionary<Guid, ImageInfo>>,
                RegistryDto>
            DefaultFactory = new RegistryFactory();

        public sealed class RegistryFactory : IManagedCRDTFactory<ItemInfoCollectionsRegistry,
            ManagedCrdtRegistry<NodeId,
                IndexId,
                ImageInfoLwwRegistry,
                ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
                IReadOnlyDictionary<Guid, ImageInfo>,
                ImageInfoLwwRegistry.LastWriteWinsDto,
                ImageInfoLwwRegistry.ItemInfoLwwRegistryFactory>,
            IReadOnlyDictionary<IndexId, IReadOnlyDictionary<Guid, ImageInfo>>, RegistryDto>
        {
            /// <inheritdoc />
            public ItemInfoCollectionsRegistry Create(RegistryDto registryDto, string instanceId) => new(registryDto, instanceId);

            /// <inheritdoc />
            public ItemInfoCollectionsRegistry Create(string instanceId) => new(instanceId);
        }
    }
}