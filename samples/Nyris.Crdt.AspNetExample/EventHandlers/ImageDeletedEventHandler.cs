using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.AspNetExample.Mongo;
using Nyris.Crdt.Distributed.Model;
using Nyris.EventBus.Subscribers;

namespace Nyris.Crdt.AspNetExample.EventHandlers
{
    internal sealed class ImageDeletedEventHandler : ImageEventHandler<ImageDeletedEvent>
    {
        private readonly MyContext _context;
        private readonly NodeId _thisNodeId;

        /// <inheritdoc />
        public ImageDeletedEventHandler(ILogger<ImageDeletedEventHandler> logger, MyContext context, NodeInfo thisNode, MongoContext mongoContext)
            : base(logger, mongoContext)
        {
            _context = context;
            _thisNodeId = thisNode.Id;
        }

        /// <inheritdoc />
        public override HandlerType HandlerType => HandlerType.Consumer;

        /// <inheritdoc />
        protected override async Task TryHandleAsync(ImageDeletedEvent message, DateTime createdEvent)
        {
            var indexId = CollectionId.FromGuid(message.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwCollection(message.IndexId.ToString("N"))));
            await index.RemoveAsync(ImageGuid.FromGuid(message.ImageUuid), createdEvent);
        }
    }
}
