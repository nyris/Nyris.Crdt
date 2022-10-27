using Nyris.Crdt.Managed;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.AspNetExampleV2;


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
        if (Environment.GetEnvironmentVariable("NODE_NAME") == "node-1")
        {
            await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);
            _host.StopApplication();
        }

        if (Environment.GetEnvironmentVariable("NODE_NAME") == "node-0")
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                
            _logger.LogInformation("Creating a crdt here");
            var crdt = await _cluster.CreateAsync<ManagedSet>(InstanceId.FromString("set1"), stoppingToken);

            var start = DateTime.Now;
            const int max = 100;
            for (var i = 0; i < max; ++i)
            {
                await crdt.AddAsync(i, stoppingToken);
            }
            _logger.LogInformation("Adding {max} elements done in {Duration}", 
                max, DateTime.Now - start);

            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                
                try
                {
                    var el = (double)Random.Shared.Next(0, 100);
                    var s = await crdt.RemoveAsync(el, stoppingToken);
                    
                    el = Random.Shared.Next(0, 100);
                    await crdt.AddAsync(el, stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Operation could not be done. Exception: {Message}", e.Message);
                }
            }
            
            _logger.LogInformation("Removal finished");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            // _host.StopApplication();
        }
    }
}