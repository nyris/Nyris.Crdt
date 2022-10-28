using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Benchmarks;

[MemoryDiagnoser]
// [ShortRunJob]
[SimpleJob(1, 3, 10)]
public class ObservedRemoveSetBenchmark
{
    // private readonly OptimizedObservedRemoveSetV2<Guid, double> _setV2 = new();
    // private readonly OptimizedObservedRemoveSetV3<Guid, double> _setV3 = new();
    //
    // private readonly OptimizedObservedRemoveSetV2<Guid, double> _setV2AddOnly = new();
    // private readonly OptimizedObservedRemoveSetV3<Guid, double> _setV3AddOnly = new();
    //
    // private readonly OptimizedObservedRemoveSetV2<Guid, double> _setV2HalfRemoved = new();
    // private readonly OptimizedObservedRemoveSetV3<Guid, double> _setV3HalfRemoved = new();
    //
    // private readonly HashSet<double> _referenceSet = new();
    private readonly ConcurrentBag<double> _referenceBag = new();
    // private double _toRemove;

    [Params(1000, 10000, 50000, 100000, 1000000)]
    public int Count;

    // [Params(1, 3, 10)]
    // public int NumberOfActors;

    // private Guid[] _actors = Array.Empty<Guid>();
    
    // [GlobalSetup]
    // public void InitializeData()
    // {
    //     _actors = Enumerable.Range(0, NumberOfActors).Select(_ => Guid.NewGuid()).ToArray();
    //
    //     var addedElements = new List<double>(Count);
    //     for (var i = 0; i < Count; ++i)
    //     {
    //         var value = Random.Shared.NextDouble();
    //         var actor = _actors[Random.Shared.Next(0, _actors.Length)];
    //         addedElements.Add(value);
    //         _setV2AddOnly.Add(value, actor);
    //         _setV3AddOnly.Add(value, actor);
    //         _setV2HalfRemoved.Add(value, actor);
    //         _setV3HalfRemoved.Add(value, actor);
    //     }
    //     
    //     Random.Shared.Shuffle(addedElements);
    //
    //     for (var i = 0; i < Count / 2; ++i)
    //     {
    //         _setV2HalfRemoved.Remove(addedElements[i]);
    //         _setV3HalfRemoved.Remove(addedElements[i]);
    //     }
    //
    //     _toRemove = addedElements.Last();
    // }

    // [Benchmark]
    // public OptimizedObservedRemoveSetV2<Guid, double> CreateV2() => new();
    //
    // [Benchmark]
    // public OptimizedObservedRemoveSetV3<Guid, double> CreateV3() => new();
    
    // [Benchmark]
    // public void AddV2()
    // {
    //     for (var i = 0; i < Count; ++i)
    //     {
    //         _setV2.Add(Random.Shared.NextDouble(), _actors[Random.Shared.Next(0, _actors.Length)]);
    //     }
    // }
    //
    // [Benchmark]
    // public void AddV3()
    // {
    //     for (var i = 0; i < Count; ++i)
    //     {
    //         _setV3.Add(Random.Shared.NextDouble(), _actors[Random.Shared.Next(0, _actors.Length)]);
    //     }
    // }
    //
    // [Benchmark]
    // public void RemoveV2() => _setV2AddOnly.Remove(_toRemove);
    //
    // [Benchmark]
    // public void RemoveV3() => _setV3AddOnly.Remove(_toRemove);
    //
    // [Benchmark]
    // public void AddRemoveV2()
    // {
    //     for (var i = 0; i < Count; ++i)
    //     {
    //         if (Random.Shared.NextDouble() < 0.33 && _referenceSet.Count > 0)
    //         {
    //             var toRemove = _referenceSet.First();
    //             _setV2.Remove(toRemove);
    //             _referenceSet.Remove(toRemove);
    //         }
    //         else
    //         {
    //             var toAdd = Random.Shared.NextDouble();
    //             _setV2.Add(toAdd, _actors[Random.Shared.Next(0, _actors.Length)]);
    //             _referenceSet.Add(toAdd);
    //         }
    //     }
    // }
    //
    // [Benchmark]
    // public void AddRemoveV3()
    // {
    //     for (var i = 0; i < Count; ++i)
    //     {
    //         if (Random.Shared.NextDouble() < 0.33 && _referenceSet.Count > 0)
    //         {
    //             var toRemove = _referenceSet.First();
    //             _setV3.Remove(toRemove);
    //             _referenceSet.Remove(toRemove);
    //         }
    //         else
    //         {
    //             var toAdd = Random.Shared.NextDouble();
    //             _setV3.Add(toAdd, _actors[Random.Shared.Next(0, _actors.Length)]);
    //             _referenceSet.Add(toAdd);
    //         }
    //     }
    // }
    //
    // [Benchmark]
    // public int EnumerateDeltasV2_Full() => _setV2AddOnly.EnumerateDeltaDtos().Count();
    //
    // [Benchmark]
    // public int EnumerateDeltasV3_Full() => _setV3AddOnly.EnumerateDeltaDtos().Count();
    //
    // [Benchmark]
    // public int EnumerateDeltasV2_HalfRemoved() => _setV2HalfRemoved.EnumerateDeltaDtos().Count();
    //
    // [Benchmark]
    // public int EnumerateDeltasV3_HalfRemoved() => _setV3HalfRemoved.EnumerateDeltaDtos().Count();

    
    [Benchmark]
    public void AddAndMergeConcurrentlyV2()
    {
        var finished1 = false;
        var finished2 = false;
        var set1 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var set2 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var thread1 = new Thread(() => OperateOnSetInLoop(set1, Guid.NewGuid(), ref finished1));
        var thread2 = new Thread(() => OperateOnSetInLoop(set2, Guid.NewGuid(), ref finished2));
        var thread3 = new Thread(() => MergeSetsInLoop(set1, set2, ref finished1, ref finished2));
        StartThreadsAndWait(thread1, thread2, thread3);
    }
    
    [Benchmark]
    public void AddAndMergeConcurrentlyV3()
    {
        var finished1 = false;
        var finished2 = false;
        var set1 = new ObservedRemoveSetV3<Guid, double>();
        var set2 = new ObservedRemoveSetV3<Guid, double>();
        var thread1 = new Thread(() => OperateOnSetInLoop(set1, Guid.NewGuid(), ref finished1));
        var thread2 = new Thread(() => OperateOnSetInLoop(set2, Guid.NewGuid(), ref finished2));
        var thread3 = new Thread(() => MergeSetsInLoop(set1, set2, ref finished1, ref finished2));
        StartThreadsAndWait(thread1, thread2, thread3);
    }
    
    private static void StartThreadsAndWait(params Thread[] threads)
    {
        for (var i = 0; i < threads.Length; ++i)
        {
            threads[i].Start();
        }
        
        for (var i = 0; i < threads.Length; ++i)
        {
            threads[i].Join();
        }
    }

    private void MergeSetsInLoop(
        IDeltaCrdt<ObservedRemoveCore<Guid, double>.DeltaDto,
            ObservedRemoveCore<Guid, double>.CausalTimestamp> set1,
        IDeltaCrdt<ObservedRemoveCore<Guid, double>.DeltaDto,
            ObservedRemoveCore<Guid, double>.CausalTimestamp> set2, 
        ref bool finished1, 
        ref bool finished2)
    {
        while(!finished1 || !finished2)
        {
            MergeToRight(set1, set2);
            MergeToRight(set2, set1);
        }
    }
    
    private void OperateOnSetInLoop(ObservedRemoveSetV3<Guid, double> set, Guid actor, ref bool finished)
    {
        for (var i = 0; i < Count; ++i)
        {
            switch (Random.Shared.NextDouble())
            {
                case < 0.33:
                    if (_referenceBag.TryTake(out var toRemove))
                    {
                        set.Remove(toRemove);
                    }
                    break;
                default:
                    var toAdd = Random.Shared.NextDouble(); 
                    set.Add(toAdd, actor);
                    _referenceBag.Add(toAdd);
                    break;
            }
        }

        finished = true;
    }
    
    private static void MergeToRight(IDeltaCrdt<ObservedRemoveCore<Guid, double>.DeltaDto, ObservedRemoveCore<Guid, double>.CausalTimestamp> left, 
        IDeltaCrdt<ObservedRemoveCore<Guid, double>.DeltaDto, ObservedRemoveCore<Guid, double>.CausalTimestamp> right)
    {
        foreach (var delta in left.EnumerateDeltaDtos(right.GetLastKnownTimestamp()))
        {
            right.Merge(delta);
        }
    }
    
    
    private void MergeSetsInLoop(
        IDeltaCrdt<ObservedRemoveDtos<Guid, double>.DeltaDto,
            ObservedRemoveDtos<Guid, double>.CausalTimestamp> set1,
        IDeltaCrdt<ObservedRemoveDtos<Guid, double>.DeltaDto,
            ObservedRemoveDtos<Guid, double>.CausalTimestamp> set2, 
        ref bool finished1, 
        ref bool finished2)
    {
        while(!finished1 || !finished2)
        {
            Thread.Sleep(10);
            MergeToRight(set1, set2);
            MergeToRight(set2, set1);
        }
    }
    
    private void OperateOnSetInLoop(OptimizedObservedRemoveSetV2<Guid, double> set, Guid actor, ref bool finished)
    {
        for (var i = 0; i < Count; ++i)
        {
            switch (Random.Shared.NextDouble())
            {
                case < 0.33:
                    if (_referenceBag.TryTake(out var toRemove))
                    {
                        set.Remove(toRemove);
                    }
                    break;
                default:
                    var toAdd = Random.Shared.NextDouble(); 
                    set.Add(toAdd, actor);
                    _referenceBag.Add(toAdd);
                    break;
            }
        }

        finished = true;
    }
    
    private static void MergeToRight(IDeltaCrdt<ObservedRemoveDtos<Guid, double>.DeltaDto, ObservedRemoveDtos<Guid, double>.CausalTimestamp> left, 
        IDeltaCrdt<ObservedRemoveDtos<Guid, double>.DeltaDto, ObservedRemoveDtos<Guid, double>.CausalTimestamp> right)
    {
        foreach (var delta in left.EnumerateDeltaDtos(right.GetLastKnownTimestamp()))
        {
            right.Merge(delta);
        }
    }
}