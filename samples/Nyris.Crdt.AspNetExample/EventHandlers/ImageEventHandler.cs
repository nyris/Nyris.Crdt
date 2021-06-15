using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.EventBus.Infrastructure;
using Nyris.EventBus.Subscribers;

namespace Nyris.Crdt.AspNetExample.EventHandlers
{
    internal abstract class ImageEventHandler<TMessage> : MessageHandler<TMessage> where TMessage : ImageEvent
    {
        private readonly ILogger _logger;

        protected ImageEventHandler(ILogger logger)
        {
            _logger = logger;
        }

        public override async Task HandleAsync(TMessage message, MessageContext context)
        {
            try
            {
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
