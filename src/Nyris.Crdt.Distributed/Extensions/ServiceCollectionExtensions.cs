using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.Consistency;
using Nyris.Crdt.Distributed.Strategies.Propagation;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInternals<TGrpcService>(this IServiceCollection services,
            INodeInfoProvider? nodeInfoProvider = null,
            IAsyncQueueProvider? queueProvider = null)
            where TGrpcService : class
        {
            services.AddSingleton<IChannelManager, ChannelManager<TGrpcService>>();
            services.AddHostedService<DiscoveryService<TGrpcService>>();

            services.TryAddSingleton((nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo());
            services.TryAddSingleton(_ => queueProvider ?? DefaultConfiguration.QueueProvider);
            services.TryAddSingleton<IPropagationStrategy, NextInRingPropagationStrategy>();
            services.TryAddSingleton<IConsistencyCheckTargetsSelectionStrategy, NextInRingConsistencyCheckTargetsSelectionStrategy>();

            services.AddConnectionServices<NodeSet, NodeSet.OrSetDto>();
            return services;
        }

        public static IServiceCollection AddConnectionServices<TCrdt, TDto>(this IServiceCollection services)
            where TCrdt : ManagedCRDT<TDto> =>
            services
                .AddHostedService<PropagationService<TCrdt, TDto>>()
                .AddHostedService<ConsistencyService<TCrdt, TDto>>();
    }
}