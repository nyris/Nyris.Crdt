using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Model;

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

            PartiallyReplicatedImageCollectionsRegistry = new(new InstanceId("partially-replicated"),
                logger: loggerFactory.CreateLogger<PartiallyReplicatedImageInfoCollectionsRegistry>(),
                factory: imageInfoCollectionFactory);
            ImageCollectionsRegistry = new(new InstanceId("sample-collections-registry"),
                logger: loggerFactory.CreateLogger<ImageInfoCollectionsRegistry>());

            UserObservedRemoveSet = new UserObservedRemoveSet(new InstanceId("users-or-set-collection"), logger: loggerFactory.CreateLogger<UserObservedRemoveSet>());

            Add<ImageInfoCollectionsRegistry, ImageInfoCollectionsRegistry.RegistryDto>(ImageCollectionsRegistry);
            Add<PartiallyReplicatedImageInfoCollectionsRegistry,
                PartiallyReplicatedImageInfoCollectionsRegistry.PartiallyReplicatedCrdtRegistryDto>(PartiallyReplicatedImageCollectionsRegistry);
            Add<UserObservedRemoveSet, UserObservedRemoveSet.UserSetDto>(UserObservedRemoveSet);

            IndexFactory.Register(ImageIdIndex.IndexName, () => new ImageIdIndex());
        }

        public ImageInfoCollectionsRegistry ImageCollectionsRegistry { get; }

        public PartiallyReplicatedImageInfoCollectionsRegistry PartiallyReplicatedImageCollectionsRegistry { get; }

        public UserObservedRemoveSet UserObservedRemoveSet { get; }
    }
}