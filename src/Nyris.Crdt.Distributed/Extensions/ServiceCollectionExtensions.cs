using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Strategies;

namespace Nyris.Crdt.Distributed.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInternals<TGrpcService>(this IServiceCollection services)
            where TGrpcService : class
        {
            services.AddSingleton<ChannelManager<TGrpcService>>();

            services.TryAddSingleton(NodeInfoProvider.GetMyNodeInfo());
            services.TryAddSingleton<IPropagationStrategy, PropagationStrategy>();
            return services;
        }

        public static IServiceCollection AddSender<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>(this IServiceCollection services)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : ICRDT<TImplementation, TRepresentation, TDto>
            where TGrpcService : class
            => services.AddHostedService<SenderService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>>();
    }
}