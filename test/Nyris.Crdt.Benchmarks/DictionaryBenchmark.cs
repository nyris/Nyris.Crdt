using BenchmarkDotNet.Attributes;

namespace Nyris.Crdt.Benchmarks;

[MemoryDiagnoser]
public class DictionaryBenchmark
{

    [Params(1, 2, 4, 6, 8)]
    public int Capacity { get; set; }

    private Dictionary<Guid, long> _dict = null!;
    private List<(Guid, long)> _list = null!;
    private readonly Guid _searchId = Guid.NewGuid();

    [GlobalSetup]
    public void Setup()
    {
        _dict = new Dictionary<Guid, long>(Capacity);
        _list = new List<(Guid, long)>(Capacity);
        _list.AddRange(Enumerable.Range(0, Capacity).Select(_ => (Guid.Empty, 0L)));

        var randomI = Random.Shared.Next(0, Capacity);
        for (var i = 0; i < Capacity; ++i)
        {
            if (i == randomI) continue;
            _dict.Add(Guid.NewGuid(), i);
            _list[i] = (Guid.NewGuid(), i);
        }

        _list[randomI] = (_searchId, randomI);
        _dict[_searchId] = randomI;
    }

    [Benchmark]
    public Dictionary<Guid, long> CreateDictionary()
    {
        return new Dictionary<Guid, long>(Capacity);
    }

    [Benchmark]
    public List<(Guid, long)> CreateList()
    {
        return new List<(Guid, long)>(Capacity);
    }

    [Benchmark]
    public int FindInList()
    {
        for (var j = 0; j < Capacity; ++j)
        {
            if (_list[j].Item1 == _searchId) return j;
        }

        return -1;
    }

    [Benchmark]
    public long FindInDict()
    {
        if (_dict.TryGetValue(_searchId, out var value)) return value;
        return -1;
    }
}
