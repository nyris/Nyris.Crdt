using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.Server;

namespace Nyris.Crdt.Distributed
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDistributedCrdts(this IServiceCollection services)
        {
            services.AddCodeFirstGrpc(options => options.EnableDetailedErrors = true);
            return services;
        }

        public static IEndpointRouteBuilder MapGrpcForDistributedCrdts(this IEndpointRouteBuilder endpoints)
        {
            // endpoints.MapGrpcService<DtoPassingService>();
            return endpoints;
        }
    }
}