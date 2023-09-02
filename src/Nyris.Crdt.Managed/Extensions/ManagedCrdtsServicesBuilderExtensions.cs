using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Managed.Discovery.Abstractions;
using Nyris.Crdt.Managed.Strategies.Discovery;

namespace Nyris.Crdt.Managed.Extensions;

public static class ManagedCrdtsServicesBuilderExtensions
{
    public static ManagedCrdtsServicesBuilder WithAddressListDiscovery(this ManagedCrdtsServicesBuilder builder, IReadOnlyCollection<Uri>? addresses)
    {
        if (addresses != null)
        {
            builder.Services.AddSingleton<IDiscoveryStrategy>(new AddressListDiscoveryStrategy(addresses));
        }

        return builder;
    }
}