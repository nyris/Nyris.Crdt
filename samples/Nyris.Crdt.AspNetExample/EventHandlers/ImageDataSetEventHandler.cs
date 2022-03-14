using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.AspNetExample.Mongo;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;
using Nyris.EventBus.Subscribers;

namespace Nyris.Crdt.AspNetExample.EventHandlers
{
    internal sealed class ImageDataSetEventHandler : ImageEventHandler<ImageDataSetEvent>
    {
        private readonly MyContext _context;
        private readonly NodeId _thisNodeId;
        /// <inheritdoc />
        public ImageDataSetEventHandler(ILogger<ImageDataSetEventHandler> logger, MyContext context, NodeInfo thisNode, MongoContext mongoContext)
            : base(logger, mongoContext)
        {
            _context = context;
            _thisNodeId = thisNode.Id;
        }

        /// <inheritdoc />
        public override HandlerType HandlerType => HandlerType.Consumer;

        /// <inheritdoc />
        protected override async Task TryHandleAsync(ImageDataSetEvent message, DateTime createdAt)
        {
            var collectionId = CollectionId.FromGuid(message.IndexId);

            if (!_context.PartiallyReplicatedImageCollectionsRegistry.CollectionExists(collectionId))
            {
                await _context.PartiallyReplicatedImageCollectionsRegistry
                    .TryAddCollectionAsync(collectionId, new CollectionConfig
                    {
                        Name = collectionId.ToString(),
                        IndexNames = new[] { ImageIdIndex.IndexName },
                        ShardingConfig = new ShardingConfig { NumShards = 2 }
                    }, 2);
            }

            var imageInfo = new ImageInfo(message.DownloadUri, message.ImageId);
            var operation = new AddValueOperation<ImageGuid, ImageInfo, DateTime>(ImageGuid.FromGuid(message.ImageUuid),
                imageInfo, DateTime.UtcNow);
            await _context.PartiallyReplicatedImageCollectionsRegistry
                .ApplyAsync<AddValueOperation<ImageGuid, ImageInfo, DateTime>, ValueResponse<ImageInfo>>(
                    collectionId,
                    operation);
        }
    }
}
