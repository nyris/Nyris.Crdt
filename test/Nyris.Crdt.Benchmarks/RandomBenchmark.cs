using BenchmarkDotNet.Attributes;

namespace Nyris.Crdt.Benchmarks;

[MemoryDiagnoser]
public class RandomBenchmark
{

    [Params(100, 1000, 10000, 100000)]
    public int Size { get; set; }


    [GlobalSetup]
    public void Setup()
    {
    }

    [Benchmark]
    public void Lock()
    {
    }
}