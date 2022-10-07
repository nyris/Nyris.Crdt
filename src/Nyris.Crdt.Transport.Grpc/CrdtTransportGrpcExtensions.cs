using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nyris.ManagedCrdtsV2;

namespace Nyris.Crdt.Transport.Grpc;

public static class CrdtTransportGrpcExtensions
{
    public static ManagedCrdtsServicesBuilder WithGrpcTransport(this ManagedCrdtsServicesBuilder builder)
    {
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<NodeGrpcClientPool>();
        builder.Services.TryAddSingleton<INodeClientPool>(sp => sp.GetRequiredService<NodeGrpcClientPool>());
        builder.Services.TryAddSingleton<INodeFailureNotifier>(sp => sp.GetRequiredService<NodeGrpcClientPool>());
        return builder;
    }

    public static void MapGrpcServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<NodeGrpcService>();
    }
}