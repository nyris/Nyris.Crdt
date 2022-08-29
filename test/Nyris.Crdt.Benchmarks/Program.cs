using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Nyris.Crdt.Benchmarks;

public class Program
{

    public abstract record Absract;

    public sealed record Concrete(int A) : Absract;
    
    public static void Main(string[] args)
    {
        // var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        // BenchmarkRunner.Run<RandomBenchmark>();

        Absract a = new Concrete(5);

        var b = Unsafe.As<Absract, Concrete>(ref a);
        Console.WriteLine(b.A);

        var aa = new Absract[10];
        for (var i = 0; i < aa.Length; ++i)
        {
            aa[i] = new Concrete(i * 2);
        }
        
        var bb = Unsafe.As<Absract[], ImmutableArray<Concrete>>(ref aa);
        for (var i = 0; i < bb.Length; ++i)
        {
            Console.WriteLine(bb[i]);
        }
    }
}