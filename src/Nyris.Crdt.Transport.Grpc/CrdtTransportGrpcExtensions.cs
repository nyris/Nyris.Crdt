using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.Crdt.Managed.DependencyInjection;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Transport.Grpc;

public static class CrdtTransportGrpcExtensions
{
    public static ManagedCrdtsServicesBuilder WithGrpcTransport(this ManagedCrdtsServicesBuilder builder)
    {
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<NodeGrpcClientFactory>();
        builder.Services.TryAddSingleton<INodeClientFactory>(sp => sp.GetRequiredService<NodeGrpcClientFactory>());
        builder.Services.TryAddSingleton<INodeFailureNotifier>(sp => sp.GetRequiredService<NodeGrpcClientFactory>());
        return builder;
    }

    public static void MapGrpcServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<NodeGrpcService>();
    }
}