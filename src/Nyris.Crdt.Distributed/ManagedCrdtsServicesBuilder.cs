using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Distributed.Strategies.Discovery;
using System;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Metrics;

namespace Nyris.Crdt.Distributed;

public sealed class ManagedCrdtsServicesBuilder
{
    private readonly IServiceCollection _services;

    public ManagedCrdtsServicesBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public ManagedCrdtsServicesBuilder WithKubernetesDiscovery(
        Action<KubernetesDiscoveryPodSelectionOptions> configureOptions
    )
    {
        var options = new KubernetesDiscoveryPodSelectionOptions();
        configureOptions(options);

        _services.AddSingleton(options);
        _services.AddSingleton<IDiscoveryStrategy, KubernetesDiscoveryStrategy>();
        return this;
    }

    public ManagedCrdtsServicesBuilder WithAddressListDiscovery(IReadOnlyCollection<Uri>? addresses)
    {
        if (addresses != null)
        {
            _services.AddSingleton<IDiscoveryStrategy, AddressListDiscoveryStrategy>(_ =>
                                                                                         new AddressListDiscoveryStrategy(addresses));
        }

        return this;
    }

    public ManagedCrdtsServicesBuilder WithMetrics()
    {
        _services.AddSingleton<ICrdtMetricsRegistry, CrdtMetricsRegistry>();

        return this;
    }
}
