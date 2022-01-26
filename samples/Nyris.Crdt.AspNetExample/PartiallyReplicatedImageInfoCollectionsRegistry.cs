using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.AspNetExample
{
    [RequireOperation(typeof(AddValueOperation<ImageGuid, ImageInfo, DateTime>), typeof(ValueResponse<ImageInfo>))]
    [RequireOperation(typeof(GetValueOperation<ImageGuid>), typeof(ValueResponse<ImageInfo>))]
    public sealed class PartiallyReplicatedImageInfoCollectionsRegistry
        : PartiallyReplicatedCRDTRegistry<PartiallyReplicatedImageInfoCollectionsRegistry,
            CollectionId,
            ImageInfoLwwCollectionWithSerializableOperations,
            IReadOnlyDictionary<ImageGuid, ImageInfo>,
            ImageInfoLwwCollectionWithSerializableOperations.LWWDto,
            RegistryOperation,
            RegistryOperationResponse,
            ImageInfoLwwCollectionWithSerializableOperations.ImageInfoLwwCollectionWithSerializableOperationsFactory>
    {
        /// <inheritdoc />
        public PartiallyReplicatedImageInfoCollectionsRegistry(string instanceId,
            IPartialReplicationStrategy? shardingStrategy = null,
            INodeInfoProvider? nodeInfoProvider = null)
            : base(instanceId, shardingStrategy, nodeInfoProvider)
        {
        }

        private PartiallyReplicatedImageInfoCollectionsRegistry(PartiallyReplicatedCrdtRegistryDto registryDto,
            string instanceId,
            IPartialReplicationStrategy? shardingStrategy = null,
            INodeInfoProvider? nodeInfoProvider = null)
            : base(registryDto, instanceId, shardingStrategy, nodeInfoProvider)
        {
        }

        public static readonly PartiallyReplicatedImageInfoCollectionsRegistryFactory DefaultFactory = new();

        public sealed class PartiallyReplicatedImageInfoCollectionsRegistryFactory : IManagedCRDTFactory<PartiallyReplicatedImageInfoCollectionsRegistry,
            IReadOnlyDictionary<CollectionId, IReadOnlyDictionary<ImageGuid, ImageInfo>>,
            PartiallyReplicatedCrdtRegistryDto>
        {
            /// <inheritdoc />
            public PartiallyReplicatedImageInfoCollectionsRegistry Create(PartiallyReplicatedCrdtRegistryDto registryDto,
                string instanceId) => new(registryDto, instanceId);

            /// <inheritdoc />
            public PartiallyReplicatedImageInfoCollectionsRegistry Create(string instanceId) => new(instanceId);
        }
    }
}