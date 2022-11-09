using Microsoft.Extensions.DependencyInjection;

namespace Nyris.Crdt.Managed.DependencyInjection;

public sealed class ManagedCrdtsServicesBuilder
{
    public readonly IServiceCollection Services;

    public ManagedCrdtsServicesBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
