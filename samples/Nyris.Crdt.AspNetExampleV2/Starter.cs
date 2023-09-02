using System.Collections;
using System.Diagnostics;
using Nyris.Crdt.Managed;
using Nyris.Crdt.Managed.Model;
using Nyris.Model.Ids;

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
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            _host.StopApplication();
        }

        if (Environment.GetEnvironmentVariable("NODE_NAME") == "node-0")
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            _logger.LogInformation("Creating a crdt here");
            var map = await _cluster.CreateAsync<ManagedMap>(InstanceId.FromString("map-1"), stoppingToken);

            var allVectors = new SortedList<ImageId, float[]>();

            // TODO: this might not work as well as I hoped. Needs additional checks
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                var vector = GetRandomVector();

                var result = await map.FindClosest(vector, stoppingToken);
                var actualId = Find(allVectors, vector, out var actualDotProduct);

                if (result.Id != actualId)
                {
                    _logger.LogInformation("Miss! True search result: {ImageId}, dp {DotProduct}", actualId, actualDotProduct);
                }

                var imageId = ImageId.NewImageId();
                allVectors.Add(imageId, vector);
                await map.AddAsync(imageId, vector, stoppingToken);
            }


            // var crdt = await _cluster.CreateAsync<ManagedSet>(InstanceId.FromString("set1"), stoppingToken);
            //
            // var start = DateTime.Now;
            // const int max = 100;
            // for (var i = 0; i < max; ++i)
            // {
            //     await crdt.AddAsync(i, stoppingToken);
            // }
            // _logger.LogInformation("Adding {max} elements done in {Duration}",
            //     max, DateTime.Now - start);
            //
            // while(true)
            // {
            //     await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            //
            //     try
            //     {
            //         var el = (double)Random.Shared.Next(0, 100);
            //         var s = await crdt.RemoveAsync(el, stoppingToken);
            //
            //         el = Random.Shared.Next(0, 100);
            //         await crdt.AddAsync(el, stoppingToken);
            //     }
            //     catch (Exception e)
            //     {
            //         _logger.LogInformation("Operation could not be done. Exception: {Message}", e.Message);
            //     }
            // }

#pragma warning disable CS0162 // Unreachable code detected
            _logger.LogInformation("Removal finished");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
#pragma warning restore CS0162 // Unreachable code detected
            // _host.StopApplication();
        }
    }

    private static float[] GetRandomVector()
    {
        var vector = new float[5];
        var sumSquared = 0.0F;
        for (var j = 0; j < vector.Length; ++j)
        {
            var v = Random.Shared.NextSingle();
            vector[j] = v;
            sumSquared += v * v;
        }

        for (var i = 0; i < vector.Length; ++i)
        {
            vector[i] /= sumSquared;
        }

        return vector;
    }

    private static ImageId Find(SortedList<ImageId, float[]> list, float[] value, out float dotProduct)
    {
        if (list.Count == 0)
        {
            dotProduct = float.MinValue;
            return ImageId.Empty;
        }

        dotProduct = float.MinValue;
        var index = 0;

        var values = list.Values;
        for (var i = 0; i < values.Count; ++i)
        {
            var currentDotProduct = DotProduct(value, values[i]);
            if (currentDotProduct > dotProduct)
            {
                dotProduct = currentDotProduct;
                index = i;
            }
        }

        return list.Keys[index];
    }

    private static float DotProduct(float[] l, float[] r)
    {
        Debug.Assert(l.Length == r.Length);
        var product = 0.0F;
        for (var i = 0; i < l.Length; ++i)
        {
            product += l[i] * r[i];
        }

        return product;
    }
}
