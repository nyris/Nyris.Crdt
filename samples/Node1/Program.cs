using Nyris.Crdt;

namespace Node1;

internal static class Program
{
    private static readonly ConcurrentSkipListMap<int, double> SkipList = new();
    private static readonly Random Random = new Random();

    public static async Task Main(string[] args)
    {
        // Nyris.Crdt.AspNetExample.Program.Main(args);

        var threads = new Thread[8];
        for (var i = 0; i < threads.Length; ++i)
        {
            threads[i] = new Thread(OperateOnSkipList);
            threads[i].Name = $"#{i}";
        }

        for (var i = 0; i < threads.Length; ++i)
        {
            threads[i].Start();
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
        Console.WriteLine(SkipList.GetRepresentation());

        foreach (var (key, value) in SkipList)
        {
            Console.WriteLine($"[{key}] = {value}");
        }
    }

    private static void OperateOnSkipList()
    {
        try
        {
            const int max = 100000;
            for (var i = 0; i < max; ++i)
            {
                if (i % (max / 4) == max / 4 - 1)
                    Console.WriteLine($"Thread {Thread.CurrentThread.Name}: iteration {i + 1} / {max}");
                const int spread = 50;
                SkipList.TryAdd(Random.Next(-spread, spread), Random.NextDouble());
                SkipList.TryRemove(Random.Next(-spread, spread), out _);
            }

            // Thread.Sleep(TimeSpan.FromMinutes(10));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception in {Thread.CurrentThread.Name}!\n\n{e}");
        }
    }
    
}