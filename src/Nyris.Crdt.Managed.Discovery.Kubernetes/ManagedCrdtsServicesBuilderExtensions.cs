using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Managed.Discovery.Abstractions;

namespace Nyris.Crdt.Managed.Discovery.Kubernetes;

public static class ManagedCrdtsServicesBuilderExtensions
{
    public static ManagedCrdtsServicesBuilder WithKubernetesDiscovery(this ManagedCrdtsServicesBuilder builder)
    {
        builder.Services.TryAddSingleton<IDiscoveryStrategy, KubernetesDiscoveryStrategy>();
        return builder;
    }
}