using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.Distributed.Model;
using Nyris.EventBus.Subscribers;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExample.EventHandlers
{
    internal sealed class ImageDataSetEventHandler : ImageEventHandler<ImageDataSetEvent>
    {
        private readonly MyContext _context;
        private readonly NodeId _thisNodeId;
        /// <inheritdoc />
        public ImageDataSetEventHandler(ILogger<ImageDataSetEventHandler> logger, MyContext context, NodeInfo thisNode) : base(logger)
        {
            _context = context;
            _thisNodeId = thisNode.Id;
        }

        /// <inheritdoc />
        public override HandlerType HandlerType => HandlerType.Consumer;

        /// <inheritdoc />
        protected override async Task TryHandleAsync(ImageDataSetEvent message, DateTime createdEvent)
        {
            var indexId = new IndexId(message.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwRegistry(message.IndexId.ToString("N"))));
            index.TrySet(message.ImageUuid, new ImageInfo(message.DownloadUri, message.ImageId), createdEvent, out _);
        }
    }
}