// using BenchmarkDotNet.Attributes;
// using Nyris.Crdt.Sets;
//
// namespace Nyris.Crdt.Benchmarks;
//
// [MemoryDiagnoser, ShortRunJob]
// public class ObservedRemoveSetBenchmark
// {
//     private readonly OptimizedObservedRemoveSetV2<Guid, double> _set = new();
//     private readonly OptimizedObservedRemoveSetV2<Guid, double> _set50 = new();
//     private readonly OptimizedObservedRemoveSetV2<Guid, double> _set90 = new();
//     private readonly OptimizedObservedRemoveSetV2<Guid, double> _empty = new();
//
//     [Params(100, 1000, 10000, 50000)]
//     public int SetsInitialSize;
//
//     [Params(0.1, 0.5, 0.9)] 
//     public double PercentRemoved;
//     
//     [Params(1, 10)]
//     public int NumberOfActors;
//
//     private List<Guid> _actors = new();
//     
//     [GlobalSetup]
//     public void InitializeData()
//     {
//         _actors = Enumerable.Range(0, NumberOfActors).Select(_ => Guid.NewGuid()).ToList();
//
//         for (var i = 0; i < SetsInitialSize; ++i)
//         {
//             _set.Add(Random.Shared.NextDouble(), _actors[i % NumberOfActors]);
//
//             if (i > 0 && Random.Shared.NextDouble() < PercentRemoved)
//             {
//                 _set.Remove(_set.Values.First());
//             }
//
//             if (i == (int)(0.5 * SetsInitialSize))
//             {
//                 _set50.Merge(_set.ToDto());
//             }
//             if (i == (int)(0.9 * SetsInitialSize))
//             {
//                 _set90.Merge(_set.ToDto());
//             }
//         }
//         _set.Add(3.14, _actors[0]);
//     }
//     
//     [Benchmark]
//     public void Add() => _set.Add(1.1, _actors[0]);
//
//     [Benchmark]
//     public void Remove() => _set.Remove(3.14);
//     
//     [Benchmark]
//     public void DeltaMergeIntoEmpty()
//     {
//         foreach (var dto in _set.EnumerateDeltaDtos())
//         {
//             _empty.Merge(dto);
//         }
//     }
//     
//     [Benchmark]
//     public void DeltaMergeIntoHalfSynced()
//     {
//         foreach (var dto in _set.EnumerateDeltaDtos(_set50.GetLastKnownTimestamp()))
//         {
//             _set50.Merge(dto);
//         }
//     }
//     
//     [Benchmark]
//     public void DeltaMergeInto90PSynced()
//     {
//         foreach (var dto in _set.EnumerateDeltaDtos(_set90.GetLastKnownTimestamp()))
//         {
//             _set90.Merge(dto);
//         }
//     }
//     
//     [Benchmark]
//     public void MergeIntoEmpty() => _empty.Merge(_set.ToDto());
//
//     [Benchmark]
//     public void MergeIntoHalfSynced() => _set50.Merge(_set.ToDto());
//     
//     [Benchmark]
//     public void MergeInto90PSynced() => _set90.Merge(_set.ToDto());
// }