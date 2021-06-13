using System;
using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Distributed.Strategies;

namespace Nyris.Crdt.Distributed
{
    public sealed class ManagedCrdtsServicesBuilder
    {
        private readonly IServiceCollection _services;

        public ManagedCrdtsServicesBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public ManagedCrdtsServicesBuilder WithKubernetesDiscovery(
            Action<KubernetesDiscoveryPodSelectionOptions> configureOptions)
        {
            var options = new KubernetesDiscoveryPodSelectionOptions();
            configureOptions(options);

            _services.AddSingleton<KubernetesDiscoveryPodSelectionOptions>();
            _services.AddSingleton<IDiscoveryStrategy, KubernetesDiscoveryStrategy>();
            return this;
        }
    }
}