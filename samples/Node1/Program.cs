using Nyris.Crdt;

namespace Node1;

internal static class Program
{
    private static readonly ConcurrentSkipListMap<int, double> SkipList = new();
    private static readonly Random Random = new Random();

    public static async Task Main(string[] args)
    {
        // Nyris.Crdt.AspNetExample.Program.Main(args);

        var time = DateTime.Now;
        var type = Type.GetType("Node1.Program");
        Console.WriteLine($"After type: {type}, took {DateTime.Now - time}");
    }
}