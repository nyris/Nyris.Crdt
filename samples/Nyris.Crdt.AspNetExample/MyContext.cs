using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class MyContext : ManagedCrdtContext
    {
        public MyContext(ILogger<MyContext> logger, ILoggerFactory loggerFactory) : base(logger)
        {
            DefaultConfiguration.ResponseCombinator = new ResponseCombinator(
                loggerFactory.CreateLogger<ResponseCombinator>());

            var imageInfoCollectionFactory = new ImageInfoLwwCollectionWithSerializableOperations
                .ImageInfoLwwCollectionWithSerializableOperationsFactory(
                    logger: loggerFactory.CreateLogger<ImageInfoLwwCollectionWithSerializableOperations>());

            PartiallyReplicatedImageCollectionsRegistry = new("partially-replicated",
                logger: loggerFactory.CreateLogger<PartiallyReplicatedImageInfoCollectionsRegistry>(),
                factory: imageInfoCollectionFactory);
            ImageCollectionsRegistry = new("sample-collections-registry",
                logger: loggerFactory.CreateLogger<ImageInfoCollectionsRegistry>());

            Add<ImageInfoCollectionsRegistry, ImageInfoCollectionsRegistry.RegistryDto>(ImageCollectionsRegistry);
            Add<PartiallyReplicatedImageInfoCollectionsRegistry,
				PartiallyReplicatedImageInfoCollectionsRegistry.PartiallyReplicatedCrdtRegistryDto>(PartiallyReplicatedImageCollectionsRegistry);

            IndexFactory.Register(ImageIdIndex.IndexName, () => new ImageIdIndex());
        }

        public ImageInfoCollectionsRegistry ImageCollectionsRegistry { get; }

        public PartiallyReplicatedImageInfoCollectionsRegistry PartiallyReplicatedImageCollectionsRegistry { get; }
    }
}