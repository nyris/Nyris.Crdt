using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Nyris.Extensions.AspNetCore.Hosting;

namespace Nyris.Crdt.AspNetExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            NyrisWebHost
                .CreateCustom<Startup>(args)
                .WithStandardAppConfiguration()
                .WithHealthChecks()
                .WithTelemetry()
                .WithMetrics()
                .AsBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(4999, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                        options.ListenAnyIP(5000, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
                    });
                });
    }
}