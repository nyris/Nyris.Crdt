using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.AspNetExample
{
    [RequireOperation(typeof(AddValueOperation<ImageGuid, ImageInfo, DateTime>), typeof(ValueResponse<ImageInfo>))]
    [RequireOperation(typeof(GetValueOperation<ImageGuid>), typeof(ValueResponse<ImageInfo>))]
    [RequireOperation(typeof(FindIdsOperation), typeof(ValueResponse<IList<ImageGuid>>))]
    public sealed class PartiallyReplicatedImageInfoCollectionsRegistry
        : PartiallyReplicatedCRDTRegistry<CollectionId,
            ImageInfoLwwCollectionWithSerializableOperations,
            ImageGuid,
            ImageInfo,
            ImageInfoLwwCollectionWithSerializableOperations.LastWriteWinsDto,
            RegistryOperation,
            RegistryOperationResponse,
            ImageInfoLwwCollectionWithSerializableOperations.ImageInfoLwwCollectionWithSerializableOperationsFactory>
    {
        /// <inheritdoc />
        public PartiallyReplicatedImageInfoCollectionsRegistry(string instanceId,
            ILogger? logger = null,
            IPartialReplicationStrategy? partialReplicationStrategy = null,
            INodeInfoProvider? nodeInfoProvider = null,
			IAsyncQueueProvider? queueProvider = null,
			IChannelManager? channelManager = null,
            ImageInfoLwwCollectionWithSerializableOperations.ImageInfoLwwCollectionWithSerializableOperationsFactory? factory = null)
            : base(instanceId,
                logger: logger,
                partialReplicationStrategy: partialReplicationStrategy,
                nodeInfoProvider: nodeInfoProvider,
				queueProvider: queueProvider,
				channelManager: channelManager,
                factory: factory)
        {
        }

        public static readonly PartiallyReplicatedImageInfoCollectionsRegistryFactory DefaultFactory = new();

        public sealed class PartiallyReplicatedImageInfoCollectionsRegistryFactory : IManagedCRDTFactory<PartiallyReplicatedImageInfoCollectionsRegistry,
            PartiallyReplicatedCrdtRegistryDto>
        {
            private readonly ILogger? _logger;

            private readonly ImageInfoLwwCollectionWithSerializableOperations.
                ImageInfoLwwCollectionWithSerializableOperationsFactory? _factory;

            public PartiallyReplicatedImageInfoCollectionsRegistryFactory()
            {
            }

            public PartiallyReplicatedImageInfoCollectionsRegistryFactory(ILogger? logger, ImageInfoLwwCollectionWithSerializableOperations.
                ImageInfoLwwCollectionWithSerializableOperationsFactory? factory)
            {
                _logger = logger;
                _factory = factory;
                _logger?.LogDebug("{FactoryName} created with logger", nameof(PartiallyReplicatedImageInfoCollectionsRegistryFactory));
            }

            /// <inheritdoc />
            public PartiallyReplicatedImageInfoCollectionsRegistry Create(string instanceId)
                => new(instanceId, logger: _logger, factory: _factory);
        }
    }
}