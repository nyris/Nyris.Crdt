using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using System;

namespace Nyris.Crdt.AspNetExample;

public sealed class ImageInfoLwwCollection : ManagedLastWriteWinsDeltaRegistry<ImageGuid, ImageInfo, DateTime>
{
    /// <inheritdoc />
    public ImageInfoLwwCollection(InstanceId id,
        IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger) { }

    public sealed class ImageInfoLwwCollectionFactory : IManagedCRDTFactory<ImageInfoLwwCollection, LastWriteWinsDto>
    {
        private readonly IAsyncQueueProvider? _queueProvider;
        private readonly ILogger? _logger;

        public ImageInfoLwwCollectionFactory(IAsyncQueueProvider? queueProvider = null, ILogger? logger = null)
        {
            _queueProvider = queueProvider;
            _logger = logger;
        }

        public ImageInfoLwwCollectionFactory() { }

        /// <inheritdoc />
        public ImageInfoLwwCollection Create(InstanceId instanceId) => new ImageInfoLwwCollection(instanceId,
            queueProvider: _queueProvider,
            logger: _logger);
    }
}
