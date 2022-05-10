using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;
using Nyris.Crdt.Distributed.Utils;
using System;
using System.Collections.Generic;

namespace Nyris.Crdt.AspNetExample;

[RequireOperation(typeof(AddValueOperation<ImageGuid, ImageInfo, DateTime>), typeof(ValueResponse<ImageInfo>))]
[RequireOperation(typeof(DeleteImageOperation), typeof(ValueResponse<bool>))]
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
    public PartiallyReplicatedImageInfoCollectionsRegistry(InstanceId instanceId,
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
            factory: factory) { }
}
