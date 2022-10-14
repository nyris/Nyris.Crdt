using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Transport.Abstractions;
using Nyris.ManagedCrdtsV2.Services;
using Nyris.ManagedCrdtsV2.Services.Hosted;
using Nyris.ManagedCrdtsV2.Strategies.Distribution;
using Nyris.ManagedCrdtsV2.Strategies.NodeSelection;

namespace Nyris.ManagedCrdtsV2;

public static class ManagedCrdtsServiceCollectionExtensions
{
    public static ManagedCrdtsServicesBuilder AddManagedCrdts(this IServiceCollection collection)
    {
        collection
            .AddMemoryCache()
            .AddHostedService<NodeFailureSubscriptionsInitializer>()
            .AddHostedService<DiscoveryService>()
            .AddHostedService<SynchronizationAndRelocationService>()
            .AddSingleton<IManagedCrdtFactory, ManagedCrdtFactory>()
            .AddSingleton<INodeInfoProvider, NodeInfoProvider>()
            .AddSingleton<INodeSelectionStrategy, NextInRingSelectionStrategy>()
            .AddSingleton<IDistributionStrategy, DistributionStrategy>()
            .AddSingleton<IMetadataPropagationService, MetadataPropagationService>()
            .AddSingleton(sp => sp.GetRequiredService<INodeInfoProvider>().GetMyNodeInfo())
            .AddSingleton<Cluster>()
            .AddSingleton<ICluster>(sp => sp.GetRequiredService<Cluster>())
            .AddSingleton<IClusterMetadataManager>(sp => sp.GetRequiredService<Cluster>())
            .AddSingleton<IReplicaDistributor>(sp => sp.GetRequiredService<Cluster>())
            .AddSingleton<IManagedCrdtProvider>(sp => sp.GetRequiredService<Cluster>())
            .AddSingleton<INodeFailureObserver>(sp => sp.GetRequiredService<Cluster>());

        return new ManagedCrdtsServicesBuilder(collection);
    }
}