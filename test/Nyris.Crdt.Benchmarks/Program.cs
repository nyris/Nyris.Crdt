using BenchmarkDotNet.Running;

namespace Nyris.Crdt.Benchmarks;

public class Program
{

    public static void Main(string[] args)
    {
        // BenchmarkRunner.Run(typeof(Program).Assembly);
        // BenchmarkRunner.Run<SkipListMapBenchmark>();

        BenchmarkRunner.Run<ObservedRemoveSetBenchmark>();
    }
}
