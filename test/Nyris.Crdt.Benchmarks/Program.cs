using BenchmarkDotNet.Running;

namespace Nyris.Crdt.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}