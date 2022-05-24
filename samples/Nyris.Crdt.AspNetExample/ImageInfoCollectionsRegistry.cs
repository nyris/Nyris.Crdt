using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Metrics;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.AspNetExample;

public sealed class ImageInfoCollectionsRegistry : ManagedCrdtRegistry<NodeId,
    CollectionId,
    ImageInfoLwwCollection,
    ImageInfoLwwCollection.LastWriteWinsDto,
    ImageInfoLwwCollection.ImageInfoLwwCollectionFactory>
{
    /// <inheritdoc />
    public ImageInfoCollectionsRegistry(InstanceId id,
        IAsyncQueueProvider? queueProvider = null,
        ImageInfoLwwCollection.ImageInfoLwwCollectionFactory? factory = null,
        ILogger? logger = null, ICrdtMetricsRegistry? metricsRegistry = null) : base(id, queueProvider: queueProvider, factory: factory,
        logger: logger, metricsRegistry: metricsRegistry) { }

    public static readonly RegistryFactory DefaultFactory = new RegistryFactory();

    public sealed class RegistryFactory : IManagedCRDTFactory<ImageInfoCollectionsRegistry,
        RegistryDto>
    {
        /// <inheritdoc />
        public ImageInfoCollectionsRegistry Create(InstanceId instanceId) => new(instanceId);
    }
}
