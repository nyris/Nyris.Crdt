using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.Consistency;
using Nyris.Crdt.Distributed.Strategies.Propagation;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInternals<TGrpcService>(this IServiceCollection services,
            INodeInfoProvider? nodeInfoProvider = null)
            where TGrpcService : class
        {
            services.AddSingleton<IChannelManager, ChannelManager<TGrpcService>>();
            services.AddHostedService<DiscoveryService<TGrpcService>>();

            services.TryAddSingleton((nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo());
            services.TryAddSingleton<IPropagationStrategy, NextInRingPropagationStrategy>();
            services.TryAddSingleton<IConsistencyCheckTargetsSelectionStrategy, NextInRingConsistencyCheckTargetsSelectionStrategy>();

            services.AddConnectionServices<NodeSet, HashSet<NodeInfo>, NodeSet.OrSetDto>();
            return services;
        }

        public static IServiceCollection AddConnectionServices<TCrdt, TRepresentation, TDto>(this IServiceCollection services)
            where TCrdt : ManagedCRDT<TCrdt, TRepresentation, TDto>
            => services
                .AddHostedService<PropagationService<TCrdt, TRepresentation, TDto>>()
                .AddHostedService<ConsistencyService<TCrdt, TRepresentation, TDto>>();
    }
}