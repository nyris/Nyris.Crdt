using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Managed.ManagedCrdts.Factory;
using Nyris.Crdt.Managed.Services;
using Nyris.Crdt.Managed.Services.Hosted;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Managed.Strategies.Distribution;
using Nyris.Crdt.Managed.Strategies.NodeSelection;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.Extensions;

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