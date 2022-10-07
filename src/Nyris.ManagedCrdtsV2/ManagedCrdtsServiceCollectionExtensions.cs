using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.ManagedCrdtsV2;

public static class ManagedCrdtsServiceCollectionExtensions
{
    public static ManagedCrdtsServicesBuilder AddManagedCrdts(this IServiceCollection collection)
    {
        collection
            .AddMemoryCache()
            .AddHostedService<NodeFailureSubscriptionsInitializer>()
            .AddHostedService<DiscoveryService>()
            .AddSingleton<IManagedCrdtFactory, ManagedCrdtFactory>()
            .AddSingleton<INodeInfoProvider, NodeInfoProvider>()
            .AddSingleton<INodeSelectionStrategy, NextInRingSelectionStrategy>()
            .AddSingleton<IDistributionStrategy, DistributionStrategy>()
            .AddSingleton<IMetadataPropagationService, MetadataPropagationService>()
            .AddSingleton(sp => sp.GetRequiredService<INodeInfoProvider>().GetMyNodeInfo())
            .AddSingleton<ClusterManager>()
            .AddSingleton<IClusterMetadataManager>(sp => sp.GetRequiredService<ClusterManager>())
            .AddSingleton<IReplicaDistributor>(sp => sp.GetRequiredService<ClusterManager>())
            .AddSingleton<IManagedCrdtProvider>(sp => sp.GetRequiredService<ClusterManager>());

        return new ManagedCrdtsServicesBuilder(collection);
    }
}