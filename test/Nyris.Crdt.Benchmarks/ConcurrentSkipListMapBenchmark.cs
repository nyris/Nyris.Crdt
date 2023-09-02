using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;

namespace Nyris.Crdt.Benchmarks;

[MemoryDiagnoser, ThreadingDiagnoser]
public class ConcurrentSkipListMapBenchmark
{
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<long, double> _dict = new();
    private readonly ConcurrentSkipListMap<long, double> _map = new();

    [Params(100, 1000, 10000, 100000)]
    public int Size { get; set; }


    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Size; ++i)
        {
            _map.TryAdd(i, i);
            _dict.TryAdd(i, i);
        }
    }

    [Benchmark]
    public void InsertRemoveEnumerate_SkipList()
    {
        var insertThread = new Thread(_ =>
        {
            for (var i = 0; i < Size; ++i)
            {
                _map.TryAdd(_random.NextInt64(0, Size), _random.NextDouble());
            }
        });

        var removeThread = new Thread(_ =>
        {
            for (var i = 0; i < Size; ++i)
            {
                _map.TryRemove(_random.NextInt64(0, Size), out var v);
            }
        });

        var enumerateThread = new Thread(_ =>
        {
            foreach (var (key, value) in _map)
            {
                // do nothing
            }
        });

        insertThread.Start();
        removeThread.Start();
        enumerateThread.Start();

        insertThread.Join();
        removeThread.Join();
        enumerateThread.Join();
    }

    [Benchmark]
    public void InsertRemoveEnumerate_ConcurrentDict()
    {
        var insertThread = new Thread(_ =>
        {
            for (var i = 0; i < Size; ++i)
            {
                _dict.TryAdd(_random.NextInt64(0, Size), _random.NextDouble());
            }
        });

        var removeThread = new Thread(_ =>
        {
            for (var i = 0; i < Size; ++i)
            {
                _dict.TryRemove(_random.NextInt64(0, Size), out var v);
            }
        });

        var enumerateThread = new Thread(_ =>
        {
            foreach (var (key, value) in _dict)
            {
                // do nothing
            }
        });

        insertThread.Start();
        removeThread.Start();
        enumerateThread.Start();

        insertThread.Join();
        removeThread.Join();
        enumerateThread.Join();
    }

    [Benchmark]
    public void ParallelInsert_SkipList()
    {
        var map = new ConcurrentSkipListMap<long, double>();
        Parallel.For(0, Size, i =>
        {
            map.TryAdd(i, i);
        });
    }

    [Benchmark]
    public void ParallelInsert_ConcurrentDict()
    {
        var map = new ConcurrentDictionary<long, double>();
        Parallel.For(0, Size, i =>
        {
            map.TryAdd(i, i);
        });
    }
}