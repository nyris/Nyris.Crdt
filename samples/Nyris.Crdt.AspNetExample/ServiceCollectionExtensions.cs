using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.AspNetExample.EventHandlers;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.EventBus;
using Nyris.EventBus.Subscribers;

namespace Nyris.Crdt.AspNetExample
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMessageHandling(this IServiceCollection services,
            IConfiguration configuration)
        {
            return services
                .AddEventHandler<ImageDataSetEvent, ImageDataSetEventHandler>(
                    configuration.GetSection("in:imageDataSet"))
                .AddEventHandler<ImageDeletedEvent, ImageDeletedEventHandler>(
                    configuration.GetSection("in:imageDeleted"));
        }

        private static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services,
            IConfigurationSection configuration)
            where TEvent : class
            where THandler : MessageHandler<TEvent>
        {
            var exchangeName = configuration.GetValue<string>("subscription:exchange");
            var routingKey = configuration.GetValue<string>("subscription:routingKey");

            services.AddSubscriber<TEvent, THandler>(
                exchangeName,
                routingKey,
                subscription => configuration.GetSection("subscription").Bind(subscription),
                queue => configuration.GetSection("queue").Bind(queue),
                exchange => configuration.GetSection("exchange").Bind(exchange)
            );

            return services;
        }
    }
}