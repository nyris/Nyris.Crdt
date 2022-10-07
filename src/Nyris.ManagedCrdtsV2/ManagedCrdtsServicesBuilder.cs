using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Distributed.Metrics;
using Nyris.Crdt.Distributed.Strategies.Discovery;

namespace Nyris.ManagedCrdtsV2;

public sealed class ManagedCrdtsServicesBuilder
{
    public readonly IServiceCollection Services;

    public ManagedCrdtsServicesBuilder(IServiceCollection services)
    {
        Services = services;
    }

    // public ManagedCrdtsServicesBuilder WithKubernetesDiscovery(
    //     Action<KubernetesDiscoveryPodSelectionOptions> configureOptions
    // )
    // {
    //     var options = new KubernetesDiscoveryPodSelectionOptions();
    //     configureOptions(options);
    //
    //     _services.AddSingleton(options);
    //     _services.AddSingleton<IDiscoveryStrategy, KubernetesDiscoveryStrategy>();
    //     return this;
    // }

    public ManagedCrdtsServicesBuilder WithAddressListDiscovery(IReadOnlyCollection<Uri>? addresses)
    {
        if (addresses != null)
        {
            Services.AddSingleton<IDiscoveryStrategy>(new AddressListDiscoveryStrategy(addresses));
        }

        return this;
    }

    public ManagedCrdtsServicesBuilder WithMetrics(Action<MetricsOptions>? configureOptions = null)
    {
        var options = new MetricsOptions();

        configureOptions?.Invoke(options);

        Services.AddSingleton(options);
        Services.AddSingleton<ICrdtMetricsRegistry, CrdtMetricsRegistry>();

        return this;
    }
}
