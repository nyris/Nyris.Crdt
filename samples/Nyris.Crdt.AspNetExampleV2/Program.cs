using Nyris.Crdt.AspNetExampleV2;
using Nyris.Crdt.Managed;
using Nyris.Crdt.Managed.Extensions;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Serialization.MessagePack;
using Nyris.Crdt.Transport.Grpc;

if ("node-3" == Environment.GetEnvironmentVariable("NODE_NAME"))
{
    await Task.Delay(TimeSpan.FromSeconds(30));
}

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(c =>
    {
        c.TimestampFormat = "[HH:mm:ss] ";
    }))
    .AddHostedService<Starter>()
    .AddManagedCrdts()
        .WithGrpcTransport()
        .WithMessagePackSerialization()
        .WithAddressListDiscovery(new[]
        {
            new Uri("http://nyriscrdt-node-0-1:8080"), 
            new Uri("http://nyriscrdt-node-1-1:8080"), 
            new Uri("http://nyriscrdt-node-2-1:8080")
        }
        .Where(uri => !uri.AbsoluteUri.Contains(Environment.GetEnvironmentVariable("NODE_NAME") ?? "&&illegal&&"))
        .ToList());

var app = builder.Build();

app.MapGrpcServices();
app.Run();


namespace Nyris.Crdt.AspNetExampleV2
{
    public class Starter : BackgroundService
    {
        private readonly ICluster _cluster;
        private readonly ILogger<Starter> _logger;
        private readonly IHostApplicationLifetime _host;

        public Starter(ICluster cluster, ILogger<Starter> logger, IHostApplicationLifetime host)
        {
            _cluster = cluster;
            _logger = logger;
            _host = host;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (Environment.GetEnvironmentVariable("NODE_NAME") == "node-0")
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                
                _logger.LogInformation("Creating a crdt here");
                var crdt = await _cluster.CreateAsync<ManagedObservedRemoveSet<int>>(InstanceId.FromString("set1"),
                    stoppingToken);

                var start = DateTime.Now;
                for (var i = 0; i < 500; ++i)
                {
                    await crdt.AddAsync(i, stoppingToken);
                }
                _logger.LogInformation("Adding 500 elements done in {Duration}", DateTime.Now - start);

                await Task.Delay(TimeSpan.FromSeconds(70), stoppingToken);
                _host.StopApplication();
            }
        }
    }
}