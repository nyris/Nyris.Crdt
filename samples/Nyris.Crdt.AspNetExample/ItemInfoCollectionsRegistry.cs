using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ItemInfoCollectionsRegistry : ManagedCrdtRegistry<NodeId,
        Guid,
        ImageInfoLwwRegistry,
        ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
        Dictionary<Guid, ImageInfo>,
        ImageInfoLwwRegistry.LastWriteWinsDto,
        ImageInfoLwwRegistry.ItemInfoLwwRegistryFactory>
    {
        /// <inheritdoc />
        public ItemInfoCollectionsRegistry(string id) : base(id)
        {
        }

        private ItemInfoCollectionsRegistry(RegistryDto registryDto) : base(registryDto)
        {
        }

        /// <inheritdoc />
        public override string TypeName => nameof(ItemInfoCollectionsRegistry);

        public static readonly IManagedCRDTFactory<ItemInfoCollectionsRegistry,
                ManagedCrdtRegistry<NodeId,
                    Guid,
                    ImageInfoLwwRegistry,
                    ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
                    Dictionary<Guid, ImageInfo>,
                    ImageInfoLwwRegistry.LastWriteWinsDto,
                    ImageInfoLwwRegistry.ItemInfoLwwRegistryFactory>,
                Dictionary<Guid, Dictionary<Guid, ImageInfo>>, RegistryDto>
            DefaultFactory = new RegistryFactory();

        public sealed class RegistryFactory : IManagedCRDTFactory<ItemInfoCollectionsRegistry,
            ManagedCrdtRegistry<NodeId,
                Guid,
                ImageInfoLwwRegistry,
                ManagedLastWriteWinsDeltaRegistry<Guid, ImageInfo, DateTime>,
                Dictionary<Guid, ImageInfo>,
                ImageInfoLwwRegistry.LastWriteWinsDto,
                ImageInfoLwwRegistry.ItemInfoLwwRegistryFactory>,
            Dictionary<Guid, Dictionary<Guid, ImageInfo>>, RegistryDto>
        {
            /// <inheritdoc />
            public ItemInfoCollectionsRegistry Create(RegistryDto registryDto) => new(registryDto);
        }
    }
}