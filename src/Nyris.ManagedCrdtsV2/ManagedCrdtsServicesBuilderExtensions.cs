using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Managed.Discovery.Abstractions;
using Nyris.ManagedCrdtsV2.Strategies.Discovery;

namespace Nyris.ManagedCrdtsV2;

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