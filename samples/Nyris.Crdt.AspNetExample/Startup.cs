using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Newtonsoft.Json;
using Nyris.Crdt.AspNetExample.Mongo;
using Nyris.Crdt.AspNetExample.Services;
using Nyris.Crdt.Distributed;
using Nyris.EventBus.AspNetCore;
using Serilog;

namespace Nyris.Crdt.AspNetExample
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder => builder.AddSerilog());

            services.AddGrpc();
            services.AddManagedCrdts<MyContext>()
                .WithKubernetesDiscovery(options =>
                {
                    options.Namespaces = new[] { "distributed-prototype-test" };
                })
                .WithAddressListDiscovery(Configuration.GetSection("ManualDiscovery").Get<List<Uri>>());

            if (Configuration.GetValue<bool>("EnableRabbitMq"))
            {
                services.AddRabbitMqEasyNetQForAspNetCore(Configuration);
                services.AddMessageHandling(Configuration.GetSection("Messaging"));
            }

            services.AddCors(o => o.AddPolicy("all", cb =>
            {
                cb.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
            }));

            services.AddControllers()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    options.SerializerSettings.MissingMemberHandling = MissingMemberHandling.Error;
                });

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Distributed CRDTs sample app",
                    Version = "v1",
                    Contact = new OpenApiContact{Email = "nikita@nyris.io", Name = "Nikita Chizhov"}
                });
            });
            services.Configure<MongoConfiguration>(c => Configuration.GetSection("MongoDb").Bind(c));
            services.AddTransient(sp => sp.GetRequiredService<IOptions<MongoConfiguration>>().Value);

            var mongoUrl = new MongoUrl(Configuration.GetSection("MongoDb:connectionString").Value);
            services
                .AddSingleton<IMongoClient>(new MongoClient(mongoUrl))
                .AddSingleton<MongoContext>();

            services.AddMetrics();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors("all");
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<GrpcService>();
                endpoints.MapControllers();
                endpoints.MapManagedCrdtService();
                endpoints.MapHealthChecks("/health");
            });
        }
    }
}
