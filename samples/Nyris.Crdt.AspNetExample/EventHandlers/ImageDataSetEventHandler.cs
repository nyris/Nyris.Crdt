using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.AspNetExample.Mongo;
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
        protected override async Task TryHandleAsync(ImageDataSetEvent message, DateTime createdEvent)
        {
            var indexId = CollectionId.FromGuid(message.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwCollection(message.IndexId.ToString("N"))));
            await index.SetAsync(ImageGuid.FromGuid(message.ImageUuid), new ImageInfo(message.DownloadUri, message.ImageId), createdEvent);
        }
    }
}
