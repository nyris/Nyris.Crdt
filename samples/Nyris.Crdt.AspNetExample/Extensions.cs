using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Distributed;
using ProtoBuf.Grpc.Server;

namespace Nyris.Crdt.AspNetExample
{
    internal static class Extensions
    {
        // public static IServiceCollection AddManagedCrdts<TContext>(this IServiceCollection services)
        //     where TContext : ManagedCrdtContext
        // {
        //     services.AddCodeFirstGrpc(options =>
        //     {
        //         options.EnableDetailedErrors = true;
        //     });
        //
        //     services.AddInternals<IDtoPassingService>();
        //
        //     services.AddSingleton<TContext>();
        //     services.AddSingleton<ManagedCrdtContext, TContext>(sp => sp.GetRequiredService<TContext>());
        //
        //     // add senders per Crdt
        //
        //     return services;
        // }
    }
}