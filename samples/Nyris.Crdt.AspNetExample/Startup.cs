using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nyris.Crdt.Distributed;
using Serilog;

namespace Nyris.Crdt.AspNetExample
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder => builder.AddSerilog());

            services.AddManagedCrdts<MyContext>()
                .WithKubernetesDiscovery(options =>
                {
                    options.Namespaces = new[] {"distributed-prototype-test"};
                });

            services.AddCors(o => o.AddPolicy("all", cb =>
            {
                cb.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
            }));

            services.AddMetrics();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseCors("all");
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapManagedCrdtService();
                endpoints.MapGet("/", async context => { await context.Response.WriteAsync("Hello World!"); });
            });
        }
    }
}