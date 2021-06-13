using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.Consistency;
using Nyris.Crdt.Distributed.Strategies.Propagation;

namespace Nyris.Crdt.Distributed.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInternals<TGrpcService>(this IServiceCollection services)
            where TGrpcService : class
        {
            services.AddSingleton<ChannelManager<TGrpcService>>();
            services.AddHostedService<DiscoveryService<TGrpcService>>();

            services.TryAddSingleton(NodeInfoProvider.GetMyNodeInfo());
            services.TryAddSingleton<IPropagationStrategy, NextInRingPropagationStrategy>();
            services.TryAddSingleton<IConsistencyCheckTargetsSelectionStrategy, NextInRingConsistencyCheckTargetsSelectionStrategy>();

            services.AddConnectionServices<TGrpcService, NodeSet,
                    ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>,
                    ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>.OrSetDto>();
            return services;
        }

        public static IServiceCollection AddConnectionServices<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>(this IServiceCollection services)
            where TCrdt : ManagedCRDT<TImplementation, TRepresentation, TDto>, TImplementation
            where TImplementation : IAsyncCRDT<TImplementation, TRepresentation, TDto>
            where TGrpcService : class
            => services
                .AddHostedService<SenderService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>>()
                .AddHostedService<ConsistencyService<TGrpcService, TCrdt, TImplementation, TRepresentation, TDto>>();
    }
}