using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.Crdt.Serialization.MessagePack;

public static class ManagedCrdtsServicesBuilderExtensions
{
    public static ManagedCrdtsServicesBuilder WithMessagePackSerialization(this ManagedCrdtsServicesBuilder builder)
    {
        builder.Services.TryAddSingleton<ISerializer, MessagePackSerializer>();
        return builder;
    }
}