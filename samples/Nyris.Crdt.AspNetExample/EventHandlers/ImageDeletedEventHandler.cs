using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.AspNetExample.Mongo;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;
using Nyris.EventBus.Subscribers;
using System;
using System.Threading.Tasks;

namespace Nyris.Crdt.AspNetExample.EventHandlers;

internal sealed class ImageDeletedEventHandler : ImageEventHandler<ImageDeletedEvent>
{
    private readonly MyContext _context;
    private readonly NodeId _thisNodeId;

    /// <inheritdoc />
    public ImageDeletedEventHandler(ILogger<ImageDeletedEventHandler> logger, MyContext context, NodeInfo thisNode,
        MongoContext mongoContext)
        : base(logger, mongoContext)
    {
        _context = context;
        _thisNodeId = thisNode.Id;
    }

    /// <inheritdoc />
    public override HandlerType HandlerType => HandlerType.Consumer;

    /// <inheritdoc />
    protected override async Task TryHandleAsync(ImageDeletedEvent message, DateTime createdAt)
    {
        var collectionId = CollectionId.FromGuid(message.IndexId);
        if (!_context.PartiallyReplicatedImageCollectionsRegistry.CollectionExists(collectionId))
        {
            return;
        }

        var operation = new DeleteImageOperation(ImageGuid.FromGuid(message.ImageUuid), createdAt, 1);
        await _context.PartiallyReplicatedImageCollectionsRegistry
            .ApplyAsync<DeleteImageOperation, ValueResponse<bool>>(collectionId, operation);
    }
}
