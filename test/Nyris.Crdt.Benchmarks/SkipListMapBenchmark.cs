using BenchmarkDotNet.Attributes;

namespace Nyris.Crdt.Benchmarks;

[MemoryDiagnoser]
public class SkipListMapBenchmark
{
    private readonly Random _random = new(1);
    
    private readonly ConcurrentSkipListMap<long, double> _map = new();
    private readonly SortedDictionary<long, double> _dict = new();
    private readonly SortedList<long, double> _list = new();
    
    [Params(500, 2000, 8000, 32000, 128000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Size; ++i)
        {
            _map.TryAdd(i, i);
            _dict.Add(i, i);
            _list.Add(i, i);
        }
    }
    
    [Benchmark]
    public void InsertSequential_SortedDict()
    {
        var dict = new SortedDictionary<long, double>();
        for (var i = 0; i < Size; ++i)
        {
            dict.Add(i, i);
        }
    }
    
    [Benchmark]
    public void InsertSequential_SortedList()
    {
        var list = new SortedList<long, double>();
        for (var i = 0; i < Size; ++i)
        {
            list.Add(i, i);
        }
    }
    
    [Benchmark]
    public void InsertSequential_SkipList()
    {
        var map = new ConcurrentSkipListMap<long, double>();
        for (var i = 0; i < Size; ++i)
        {
            map.TryAdd(i, i);
        }
    }
    
    [Benchmark]
    public void InsertRandom_SortedDict()
    {
        var dict = new SortedDictionary<long, double>();
        for (var i = 0; i < Size; ++i)
        {
            dict.Add(_random.NextInt64(), _random.NextDouble());
        }
    }
    
    [Benchmark]
    public void InsertRandom_SortedList()
    {
        var list = new SortedList<long, double>();
        for (var i = 0; i < Size; ++i)
        {
            list.Add(_random.NextInt64(), _random.NextDouble());
        }
    }
    
    [Benchmark]
    public void InsertRandom_SkipList()
    {
        var map = new ConcurrentSkipListMap<long, double>();
        for (var i = 0; i < Size; ++i)
        {
            map.TryAdd(_random.NextInt64(), _random.NextDouble());
        }
    }

    [Benchmark]
    public void Enumerate_SkipList()
    {
        foreach (var (key, value) in _map)
        {
            // do nothing
        }
    }

    [Benchmark]
    public void Enumerate_Dict()
    {
        foreach (var (key, value) in _dict)
        {
            // do nothing
        }
    }

    [Benchmark]
    public void Enumerate_List()
    {
        foreach (var (key, value) in _list)
        {
            // do nothing
        }
    }
}