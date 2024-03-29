using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.AspNetExample;

public sealed class ImageInfoLwwCollectionWithSerializableOperations
    : ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<ImageGuid,
        ImageInfo,
        DateTime>
{
    /// <inheritdoc />
    public ImageInfoLwwCollectionWithSerializableOperations(InstanceId instanceId,
        IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null)
        : base(instanceId, queueProvider: queueProvider, logger: logger) { }

    /// <inheritdoc />
    public override async Task<RegistryOperationResponse> ApplyAsync(RegistryOperation operation,
        CancellationToken cancellationToken = default)
    {
        return operation switch
        {
            DeleteImageOperation(var imageGuid, var dateTime, var propagateToNodes) => new ValueResponse<bool>(
                (await RemoveAsync(imageGuid, dateTime, propagateToNodes, cancellationToken: cancellationToken)).Deleted),
            FindIdsOperation findIdsOperation => new ValueResponse<IList<ImageGuid>>(await FindAsync(findIdsOperation.ImageId)),
            _ => await base.ApplyAsync(operation, cancellationToken)
        };
    }

    public async Task<IList<ImageGuid>> FindAsync(string imageId)
    {
        return TryGetIndex<ImageIdIndex>(ImageIdIndex.IndexName, out var index)
            ? await index.FindAsync(imageId)
            : ArraySegment<ImageGuid>.Empty;
    }

    public sealed class ImageInfoLwwCollectionWithSerializableOperationsFactory
        : IManagedCRDTFactory<ImageInfoLwwCollectionWithSerializableOperations, LastWriteWinsDto>
    {
        private readonly ILogger? _logger;
        private readonly IAsyncQueueProvider? _queueProvider;

        public ImageInfoLwwCollectionWithSerializableOperationsFactory() { }

        public ImageInfoLwwCollectionWithSerializableOperationsFactory(IAsyncQueueProvider? queueProvider = null, ILogger? logger = null)
        {
            _queueProvider = queueProvider;
            _logger = logger;
            _logger?.LogDebug("{FactoryName} created with logger",
                nameof(ImageInfoLwwCollectionWithSerializableOperationsFactory));
        }

        /// <inheritdoc />
        public ImageInfoLwwCollectionWithSerializableOperations Create(InstanceId instanceId)
            => new(instanceId, logger: _logger, queueProvider: _queueProvider);
    }
}
