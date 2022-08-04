using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using BenchmarkDotNet.Attributes;
using Nyris.Crdt.Extensions;

namespace Nyris.Crdt.Benchmarks;

[MemoryDiagnoser]
public class RandomBenchmark
{
    private readonly Guid[] _array = {
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
    };
    
    private readonly IReadOnlyList<Guid> _list = new Guid[] {
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
    };

    private readonly ImmutableArray<Guid> _immutable = ImmutableArray.Create<Guid>( new[] {
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
    });

    [Benchmark]
    public void ForArray()
    {
        var max = Guid.Empty;
        for (var i = 0; i < _array.Length; ++i)
        {
            max = max.CompareTo(_array[i]) < 0 ? _array[i] : max;
        }
    }
    
    [Benchmark]
    public void ForeachArray()
    {
        var max = Guid.Empty;
        foreach (var guid in _array)
        {
            max = max.CompareTo(guid) < 0 ? guid : max;
        }
    }
    
    [Benchmark]
    public void ForImmutable()
    {
        var max = Guid.Empty;
        for (var i = 0; i < _immutable.Length; ++i)
        {
            max = max.CompareTo(_immutable[i]) < 0 ? _immutable[i] : max;
        }
    }
    
    [Benchmark]
    public void ForeachImmutable()
    {
        var max = Guid.Empty;
        foreach (var guid in _immutable)
        {
            max = max.CompareTo(guid) < 0 ? guid : max;
        }
    }
    
    [Benchmark]
    public void ForList()
    {
        var max = Guid.Empty;
        for (var i = 0; i < _list.Count; ++i)
        {
            max = max.CompareTo(_list[i]) < 0 ? _list[i] : max;
        }
    }
    
    [Benchmark]
    public void ForeachList()
    {
        var max = Guid.Empty;
        foreach (var t in _list)
        {
            max = max.CompareTo(t) < 0 ? t : max;
        }
    }

    
    // private readonly SortedList<ulong, double> _list = new()
    // {
    //     [1] = Random.Shared.NextDouble(),
    //     [2] = Random.Shared.NextDouble(),
    //     [3] = Random.Shared.NextDouble(),
    //     [4] = Random.Shared.NextDouble(),
    //     [5] = Random.Shared.NextDouble(),
    //     [6] = Random.Shared.NextDouble(),
    //     [7] = Random.Shared.NextDouble(),
    //     [8] = Random.Shared.NextDouble(),
    // };
    //
    // private static int GetIndex<T>(SortedList<ulong, T> list, ulong key)
    // {
    //     if (list.Count == 0 || list.Keys[0] >= key) return 0;
    //     if (list.Keys[^1] < key) return list.Count;
    //         
    //     var l = 0;
    //     var r = list.Count - 1;
    //     var keys = list.Keys;
    //
    //     while (l < r)
    //     {
    //         var mid = (l + r) / 2;
    //         if (keys[mid] > key)
    //         {
    //             r = mid - 1;
    //             continue;
    //         }
    //         if (keys[mid] == key)
    //         {
    //             return mid;
    //         }
    //
    //         l = mid + 1;
    //     }
    //         
    //     return l;
    // }
    //
    // private int GetIndex(ulong key)
    // {
    //     if (_list.Count == 0 || _list.Keys[0] >= key) return 0;
    //     if (_list.Keys[^1] < key) return _list.Count;
    //         
    //     var l = 0;
    //     var r = _list.Count - 1;
    //     var keys = _list.Keys;
    //
    //     while (l < r)
    //     {
    //         var mid = (l + r) / 2;
    //         if (keys[mid] > key)
    //         {
    //             r = mid - 1;
    //             continue;
    //         }
    //         if (keys[mid] == key)
    //         {
    //             return mid;
    //         }
    //
    //         l = mid + 1;
    //     }
    //         
    //     return l;
    // }
    //
    // [Benchmark]
    // public void ComparerBased()
    // {
    //     _list.GetIndexOfFirstGreaterOrEqualKey(4UL);
    // }
    //
    // [Benchmark]
    // public void OperatorsBased()
    // {
    //     GetIndex(_list, 4UL);
    // }
    //
    // [Benchmark]
    // public void PrivateMethod()
    // {
    //     GetIndex(4UL);
    // }
    
    // [Params(1, 2, 3, 4)]
    // public int Size;
    //
    // private readonly List<double> _list;
    //
    // public RandomBenchmark()
    // {
    //     _list = new(5);
    //     for (var i = 0; i < Size; ++i)
    //     {
    //         _list.Add(Random.Shared.NextDouble());
    //     }
    // }
    //
    // [Benchmark]
    // public void JustForLoop()
    // {
    //     for (var i = 0; i < _list.Count; ++i)
    //     {
    //         _list[i] += 1;
    //     }
    // }
    //
    // [Benchmark]
    // public void ForLoopWithCheck()
    // {
    //     if (_list.Count == 1)
    //     {
    //         _list[0] += 1;
    //     }
    //     else
    //     {
    //         for (var i = 0; i < _list.Count; ++i)
    //         {
    //             _list[i] += 1;
    //         }
    //     }
    // }
    
    // private readonly ReaderWriterLockSlim _lock = new();
    // private readonly SemaphoreSlim _semaphore = new(1, 1);
    // private readonly object _object = new();
    //
    // [Benchmark]
    // public void ReaderWriter_Read()
    // {
    //     _lock.EnterReadLock();
    //     _lock.ExitReadLock();
    // }
    //
    // [Benchmark]
    // public void ReaderWriter_Write()
    // {
    //     _lock.EnterReadLock();
    //     _lock.ExitReadLock();
    // }
    //
    // [Benchmark]
    // public void SimpleLock()
    // {
    //     lock (_object)
    //     {
    //     }
    // }
    //
    // [Benchmark]
    // public void Semaphore()
    // {
    //     _semaphore.Wait();
    //     _semaphore.Release();
    // }
    
    // [Benchmark]
    // public void TypeSwitch()
    // {
    //     var o = new OperationFirst(1.0, "123");
    //     TypeSwitch(o);
    // }
    //
    // [Benchmark]
    // public void EnumSwitch()
    // {
    //     var o = new Operation(OperationType.First, 1.0, "123", null, null, null, null);
    //     EnumSwitch(o);
    // }
    //
    // [Benchmark]
    // public void TypeSwitchSecond()
    // {
    //     var o = new OperationSecond(1, "123");
    //     TypeSwitch(o);
    // }
    //
    // [Benchmark]
    // public void EnumSwitchSecond()
    // {
    //     var o = new Operation(OperationType.Second, null, null, 1, "123", null, null);
    //     EnumSwitch(o);
    // }

    private int TypeSwitch(OperationBase operation)
    {
        return operation switch
        {
            OperationFirst first => HashCode.Combine(first.One, first.Two),
            OperationSecond second => HashCode.Combine(second.One, second.Two),
            OperationThird third => HashCode.Combine(third.One, third.Two),
            _ => throw new NotImplementedException()
        };
    }
    
    private int EnumSwitch(Operation operation)
    {
        return operation.Type switch
        {
            OperationType.First => HashCode.Combine(operation.First1!.Value, operation.First2),
            OperationType.Second => HashCode.Combine(operation.Second1!.Value, operation.Second2),
            OperationType.Third => HashCode.Combine(operation.Third1, operation.Third2),
            _ => throw new NotImplementedException()
        };
    }
    
    
    private enum OperationType
    {
        First,
        Second,
        Third
    }

    private record Operation(OperationType Type, double? First1, string? First2, int? Second1, string? Second2,
        DateTime? Third1, Guid? Third2);

    private abstract record OperationBase;
    private record OperationFirst(double One, string Two) : OperationBase;
    private record OperationSecond(int One, string Two) : OperationBase;
    private record OperationThird(DateTime One, Guid Two) : OperationBase;
}