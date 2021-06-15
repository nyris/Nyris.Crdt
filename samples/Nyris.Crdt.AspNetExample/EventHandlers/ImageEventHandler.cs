using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.AspNetExample.Mongo;
using Nyris.EventBus.Infrastructure;
using Nyris.EventBus.Subscribers;

namespace Nyris.Crdt.AspNetExample.EventHandlers
{
    internal abstract class ImageEventHandler<TMessage> : MessageHandler<TMessage> where TMessage : ImageEvent
    {
        private readonly ILogger _logger;
        private readonly MongoContext _context;

        protected ImageEventHandler(ILogger logger, MongoContext context)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task HandleAsync(TMessage message, MessageContext context)
        {
            try
            {
                await _context.Images.InsertOneAsync(message.ToBson(context.Timestamp));
                await TryHandleAsync(message, context.Timestamp);
            }
            catch (OperationCanceledException)
            {
                context.MessageHandling = MessageHandling.Cancelled;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error was occured while processing an {MessageType} message",
                    typeof(TMessage).Name);
                context.MessageHandling = MessageHandling.Cancelled;
            }
        }

        protected abstract Task TryHandleAsync(TMessage message, DateTime createdEvent);
    }
}
