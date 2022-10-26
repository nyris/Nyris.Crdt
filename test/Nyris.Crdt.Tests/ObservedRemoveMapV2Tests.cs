using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Sets;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Nyris.Crdt.Tests;

public sealed class ObservedRemoveMapV2Tests
{
    private readonly ITestOutputHelper _output;
    private readonly Random _random = new(2);
        
    public ObservedRemoveMapV2Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 1, 1, false)]
    [InlineData(3, 1, 1)]
    [InlineData(3, 1, 1, false)]
    [InlineData(1, 3, 1)]
    [InlineData(1, 3, 1, false)]
    [InlineData(3, 3, 1)]
    [InlineData(3, 3, 1, false)]
    [InlineData(3, 3, 3)]
    [InlineData(3, 3, 3, false)]
    [InlineData(9, 3, 2)]
    [InlineData(9, 3, 2, false)]
    [InlineData(3, 9, 2)]
    [InlineData(3, 9, 2, false)]
    public void MergeIntoEmpty_Works(int nKeys, int nElements, int nActors, bool addPopulated = true)
    {
        var map1 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        var map2 = NewMap();
        DeltaMerge(map1, map2);
        AssertMapEquality(map1, map2);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 1, 1, false)]
    [InlineData(3, 1, 1)]
    [InlineData(3, 1, 1, false)]
    [InlineData(1, 3, 1)]
    [InlineData(1, 3, 1, false)]
    [InlineData(3, 3, 1)]
    [InlineData(3, 3, 1, false)]
    [InlineData(3, 3, 3)]
    [InlineData(3, 3, 3, false)]
    [InlineData(9, 3, 2)]
    [InlineData(9, 3, 2, false)]
    [InlineData(3, 9, 2)]
    [InlineData(3, 9, 2, false)]
    public void EqualSplitDistinctActors_SameKeys_Works(int nKeys, int nElements, int nActors, bool addPopulated = true)
    {
        var map1 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        var map2 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        DeltaMerge(map1, map2);
        AssertMapEquality(map1, map2);
        map1.Count.Should().Be(nKeys, "both map had the same keys");
    }
    
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 1, 1, false)]
    [InlineData(3, 1, 1)]
    [InlineData(3, 1, 1, false)]
    [InlineData(1, 3, 1)]
    [InlineData(1, 3, 1, false)]
    [InlineData(3, 3, 1)]
    [InlineData(3, 3, 1, false)]
    [InlineData(3, 3, 3)]
    [InlineData(3, 3, 3, false)]
    [InlineData(9, 3, 2)]
    [InlineData(9, 3, 2, false)]
    [InlineData(3, 9, 2)]
    [InlineData(3, 9, 2, false)]
    public void EqualSplitDistinctActors_DifferentKeys_Works(int nKeys, int nElements, int nActors, bool addPopulated = true)
    {
        var map1 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        var map2 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated, nKeys);
        DeltaMerge(map1, map2);
        AssertMapEquality(map1, map2);
        map1.Count.Should().Be(nKeys * 2, "maps had non-overlapping keys, so after merging they should have twice the count");
    }
    
    [Theory]
    [InlineData(3, 3, 3)]
    [InlineData(3, 3, 3, false)]
    [InlineData(9, 3, 2)]
    [InlineData(9, 3, 2, false)]
    [InlineData(3, 9, 2)]
    [InlineData(3, 9, 2, false)]
    [InlineData(12, 5, 3)]
    [InlineData(12, 5, 3, false)]
    public void EqualSplitDistinctActors_PartiallyOverlappingKeys_Works(int nKeys, int nElements, int nActors, bool addPopulated = true)
    {
        var map1 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        var map2 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated, nKeys / 2);
        DeltaMerge(map1, map2);
        AssertMapEquality(map1, map2);
        map1.Count.Should().Be(nKeys + nKeys / 2, "maps had non-overlapping keys");
    }
    
    [Theory]
    [InlineData(3, 3, 3, 1)]
    [InlineData(3, 3, 3, 1, false)]
    [InlineData(9, 3, 2, 3)]
    [InlineData(9, 3, 2, 3, false)]
    [InlineData(3, 9, 2, 2)]
    [InlineData(3, 9, 2, 2, false)]
    [InlineData(12, 5, 3, 5)]
    [InlineData(12, 5, 3, 5, false)]
    public void EqualSplitDistinctActors_DeletesBeforeMerge_Works(int nKeys, int nElements, int nActors, int nDeletes, bool addPopulated = true)
    {
        var map1 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        var map2 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated, nKeys / 2);
        
        // act
        DropRandomKeys(map1, nDeletes);
        DropRandomKeys(map2, nDeletes);
        DeltaMerge(map1, map2);
        
        map1.Count.Should().NotBe(0);
        AssertMapEquality(map1, map2);
    }
    
    [Theory]
    [InlineData(3, 3, 3, 1)]
    [InlineData(3, 3, 3, 1, false)]
    [InlineData(9, 3, 2, 3)]
    [InlineData(9, 3, 2, 3, false)]
    [InlineData(5, 9, 2, 2)]
    [InlineData(5, 9, 2, 2, false)]
    [InlineData(12, 5, 3, 8)]
    [InlineData(12, 5, 3, 8, false)]
    public void EqualSplitDistinctActors_DeletesAfterMerge_Works(int nKeys, int nElements, int nActors, int nDeletes, bool addPopulated = true)
    {
        // prepare
        var map1 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated);
        var map2 = GetMapSequentialIntKeys(nKeys, nElements, nActors, addPopulated, nKeys / 2);
        DeltaMerge(map1, map2);
        
        // act
        DropRandomKeys(map1, nDeletes);
        DropRandomKeys(map2, nDeletes);
        DeltaMerge(map1, map2);

        map1.Count.Should().NotBe(0);
        AssertMapEquality(map1, map2);
    }

    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(3, 2, 1)]
    [InlineData(1, 10, 1)]
    [InlineData(3, 10, 1)]
    [InlineData(5, 10, 1)]
    [InlineData(11, 10, 1)]
    [InlineData(100, 10, 1)]
    [InlineData(1, 100, 1)]
    [InlineData(3, 100, 1)]
    [InlineData(5, 100, 1)]
    [InlineData(11, 100, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(1, 2, 5)]
    [InlineData(3, 2, 5)]
    [InlineData(1, 10, 5)]
    [InlineData(3, 10, 5)]
    [InlineData(5, 10, 5)]
    [InlineData(11, 10, 5)]
    [InlineData(100, 10, 5)]
    [InlineData(1, 100, 5)]
    [InlineData(3, 100, 5)]
    [InlineData(5, 100, 5)]
    [InlineData(11, 100, 5)]
    [InlineData(100, 100, 5)]
    [InlineData(1, 2, 50)]
    [InlineData(3, 2, 50)]
    [InlineData(1, 10, 50)]
    [InlineData(3, 10, 50)]
    [InlineData(5, 10, 50)]
    [InlineData(11, 10, 50)]
    [InlineData(100, 10, 50)]
    [InlineData(1, 100, 50)]
    [InlineData(3, 100, 50)]
    [InlineData(5, 100, 50)]
    [InlineData(11, 100, 50)]
    // [InlineData(100, 100, 50)]
    public void RepeatedAddRemoveMerge_Works(int nActors, int nLoops, int nOperationsPerCycle)
    {
        // pre-populate maps so that mutations/removal have keys to act on.
        var map1 = GetMapSequentialIntKeys(nLoops, 3, nActors);
        var map2 = NewMap();
        DeltaMerge(map1, map2);
        var actors = Enumerable.Range(0, nActors).Select(_ => Guid.NewGuid()).ToList();

        // act
        for (var i = 0; i < nLoops; ++i)
        {
            for (var j = 0; j < nOperationsPerCycle; ++j)
            {
                MakeRandomOperation(map1, actors[_random.Next(0, actors.Count)]);
            }
            DeltaMerge(map1, map2);
            AssertMapEquality(map1, map2);
            _output.WriteLine($"Operation {i} is done");
        }
    }
    
    [Theory]
    [InlineData(3, 2, 1)]
    [InlineData(3, 10, 1)]
    [InlineData(5, 10, 1)]
    [InlineData(11, 10, 1)]
    [InlineData(100, 10, 1)]
    [InlineData(3, 100, 1)]
    [InlineData(5, 100, 1)]
    [InlineData(11, 100, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(3, 2, 5)]
    [InlineData(3, 10, 5)]
    [InlineData(5, 10, 5)]
    [InlineData(11, 10, 5)]
    [InlineData(100, 10, 5)]
    [InlineData(3, 100, 5)]
    [InlineData(5, 100, 5)]
    [InlineData(11, 100, 5)]
    [InlineData(100, 100, 5)]
    [InlineData(3, 2, 50)]
    [InlineData(3, 10, 50)]
    [InlineData(5, 10, 50)]
    [InlineData(11, 10, 50)]
    [InlineData(100, 10, 50)]
    [InlineData(3, 100, 50)]
    [InlineData(5, 100, 50)]
    [InlineData(11, 100, 50)]
    // [InlineData(100, 100, 50)]
    public void RepeatedAddRemoveMerge_TwoSided_Works(int nActors, int nLoops, int nOperationsPerCycle)
    {
        // pre-populate maps so that mutations/removal have keys to act on.
        var map1 = GetMapSequentialIntKeys(nLoops + 1, 3, nActors);
        var map2 = NewMap();
        DeltaMerge(map1, map2);
        var actors = Enumerable.Range(0, nActors).Select(_ => Guid.NewGuid()).ToList();

        // act
        for (var i = 0; i < nLoops; ++i)
        {
            var a1 = _random.Next(0, actors.Count);
            var a2 = a1 == 0 ? actors.Count - 1 : a1 - 1;
            
            for (var j = 0; j < nOperationsPerCycle; ++j)
            {
                MakeRandomOperation(map1, actors[a1]);
                MakeRandomOperation(map1, actors[a2]);
            }
            DeltaMerge(map1, map2);
            AssertMapEquality(map1, map2);
            _output.WriteLine($"Operation {i} is done");
        }
    }
    
    [Theory]
    [InlineData(2, 10)]
    [InlineData(3, 10)]
    [InlineData(6, 10)]
    [InlineData(10, 10)]
    [InlineData(30, 10)]
    public void ConcurrentMerge_Works(int nMaps, int nElements)
    {
        var maps = Enumerable
            .Range(0, nMaps)
            .Select(i => GetMapSequentialIntKeys(nElements, 3, 2, true, i * (nElements / 2)))
            .ToArray();

        // act
        Parallel.For(1, maps.Length, i =>
        {
            DeltaMerge(maps[i], maps[0]);
        });

        maps[0].Count.Should().Be((nMaps + 1) * (nElements / 2));

        // merge second time to propagate updates to all sets, not just 0-th 
        Parallel.For(1, maps.Length, i =>
        {
            DeltaMerge(maps[i], maps[0]);
        });

        // assert
        maps[0].Count.Should().Be((nMaps + 1) * (nElements / 2));
        for (var i = 1; i < maps.Length; ++i)
        {
            AssertMapEquality(maps[0], maps[i]);
        }
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 6)]
    [InlineData(4, 20)]
    [InlineData(8, 20)]
    [InlineData(16, 20)]
    [InlineData(20, 50)]
    public async Task ConcurrentMutationsAndMerges_Works(int nMaps, int nKeys)
    {
        var maps = Enumerable
            .Range(0, nMaps)
            .Select(_ => NewMap())
            .ToArray();

        var actors = new Guid[maps.Length];
        for (var i = 0; i < maps.Length; ++i)
        {
            actors[i] = Guid.NewGuid();
        }
            
        // act
        var tasks = new Task[maps.Length * 2];
        var start = DateTime.Now;
        for (var i = 0; i < maps.Length; ++i)
        {
            var next = i > 0 ? i - 1 : maps.Length - 1;
            
            // Distribute all elements between actors (include element if [element index] % [n actors] == [actor index])
            // But also add some other elements where that condition is not met, so that there is overlap
            var actorsKeys = Enumerable.Range(0, nKeys)
                .Where((_, j) => j % nMaps == i || _random.NextDouble() < 0.2)
                .ToList();

            tasks[i] = DeltaMergeContinuouslyAsync(maps[i], maps[next], 
                TimeSpan.FromMilliseconds(800),
                TimeSpan.FromMilliseconds(10));
            tasks[maps.Length + i] = AddAndRemoveContinuouslyAsync(maps[i], actorsKeys, actors[i],
                TimeSpan.FromMilliseconds(800), 
                TimeSpan.FromMilliseconds(10));
        }

        await Task.WhenAll(tasks);
        _output.WriteLine($"All tasks awaited in {DateTime.Now - start}");
        
        // merge without drops
        start = DateTime.Now;
        for (var i = 0; i < maps.Length; ++i)
        for (var j = 0; j < maps.Length; ++j)
        {
            if (i == j) continue;
            DeltaMerge(maps[i], maps[j], true);
        }
        _output.WriteLine($"All sets merged in {DateTime.Now - start}");

        // assert
        maps[0].Count.Should().NotBe(0);
        for (var i = 1; i < maps.Length; ++i)
        {
            AssertMapEquality(maps[0], maps[i]);
        }
    }

    [Fact]
    public void Test()
    {
        var map1 = NewMap();
        var map2 = NewMap();
        var map3 = NewMap();
        
        map1.AddOrMerge(Guid.NewGuid(), -1, GetRandomSet(1));
        DeltaMerge(map1, map3, true, false, false, false);

        var actor = Guid.NewGuid();
        map1.TryMutate(actor, -1, set => set.Add(_random.NextDouble(), Guid.NewGuid()), out var d);
        DeltaMerge(map1, map2, true, false, false, false);
        
        map1.TryMutate(actor, -1, set => set.Add(_random.NextDouble(), Guid.NewGuid()), out _);
        var deltas = map1.EnumerateDeltaDtos().ToArray();
        map3.Merge(deltas.First());
        map2.Merge(deltas.First());
        
        DeltaMerge(map2, map3, true, false, false, false);
        AssertMapEquality(map2, map3);
    }
    
    [Fact]
    public void Test2()
    {
        var map1 = NewMap();
        var map2 = NewMap();

        var actor = Guid.NewGuid();
        var deltas1 = map1.AddOrMerge(actor, -1, GetRandomSet(1));
        map1.TryRemove(-1, out var deltas2);
        var deltas3 = map1.AddOrMerge(actor, -1, GetRandomSet(1));

        foreach (var dto in deltas1.Concat(deltas3).Concat(deltas2))
        {
            map2.Merge(dto);
        }
        
        AssertMapEquality(map1, map2);
    }

    private void MakeRandomOperation(ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map, 
        Guid actorId, 
        int? useKeyIfPresent = null)
    {
        switch (_random.NextDouble(), map.Count)
        {
            case (< 0.25, > 0):
                var value = _random.NextDouble();
                var keyToMutate = GetKey();
                // _output.WriteLine($"Mutating map {map.Id}: key {keyToMutate}, value to add: {value}");
                map.TryMutate(actorId, 
                    keyToMutate, 
                    orSet => orSet.Add(value, actorId), // actorId), 
                    out _);
                // map.TryGet(keyToMutate, set => set.ToDto(), out var dto);
                break;
            case (< 0.5, > 0):
                keyToMutate = GetKey();
                map.TryGet(keyToMutate, orSet => orSet.Values.FirstOrDefault(), out value);
                // _output.WriteLine($"Mutating map {map.Id}: key {keyToMutate}, removing '{value}'");
                map.TryMutate(actorId, 
                    keyToMutate, 
                    orSet => orSet.Remove(value), // actorId), 
                    out _);
                break;
            case (< 0.75, > 0):
                var keyToRemove = GetKey();
                map.TryRemove(keyToRemove, out _);
                break;
            // case (< 0.8, > 0):
            //     var key = GetKey();
            //     map.TryGet(key, out var set).Should().BeTrue();
            //     value = _random.NextDouble();
            //     set!.Add(value, actorId);
            //
            //     value = _random.NextDouble(); 
            //     set.Add(value, actorId);
            //     map.AddOrMerge(actorId, key, set);
            //     break;
            default:
                var key = useKeyIfPresent ?? _random.Next();
                var set = GetRandomSet(1);
                // _output.WriteLine($"inserting into map {map.Id}: actor {actorId.ToString()[..8]}, key {key}, value: {set.Values.First()}");
                map.AddOrMerge(actorId, key, set);
                break;
        }
        
        int GetKey() => useKeyIfPresent.HasValue && map.TryGet(useKeyIfPresent.Value, out _) ? useKeyIfPresent.Value : map.Keys.First();
    }

    private void DropRandomKeys(ObservedRemoveMapV2<Guid,
        int,
        OptimizedObservedRemoveSetV3<Guid, double>,
        OptimizedObservedRemoveCore<Guid, double>.DeltaDto,
        OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map, int nDeletes)
    {
        nDeletes.Should().BeLessOrEqualTo(map.Count, "can't remove more keys then are present in map");
        var keys = map.Keys.ToArray();
        _random.Shuffle(keys);

        for (var i = 0; i < nDeletes; ++i)
        {
            map.TryRemove(keys[i], out _);
        }
    }

    private ObservedRemoveMapV2<Guid,
        int,
        OptimizedObservedRemoveSetV3<Guid, double>,
        OptimizedObservedRemoveCore<Guid, double>.DeltaDto,
        OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> NewMap()
        => new();
            // _output.BuildLoggerFor<ObservedRemoveMapV2<Guid,
            // int, 
            // OptimizedObservedRemoveSetV3<Guid, double>, 
            // OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            // OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp>>());
            // NullLogger<ObservedRemoveMapV2<Guid, int, OptimizedObservedRemoveSetV3<Guid, double>,
            //     OptimizedObservedRemoveSetV3<Guid, double>.DeltaDto,
            //     OptimizedObservedRemoveSetV3<Guid, double>.CausalTimestamp>>.Instance);  

    private OptimizedObservedRemoveSetV3<Guid, double> GetRandomSet(int nElements)
    {
        var set = new OptimizedObservedRemoveSetV3<Guid, double>();
        for (var j = 0; j < nElements; ++j)
        {
            set.Add(_random.NextDouble(), Guid.NewGuid());
        }

        return set;
    }

    private ObservedRemoveMapV2<Guid,
        int,
        OptimizedObservedRemoveSetV3<Guid, double>,
        OptimizedObservedRemoveCore<Guid, double>.DeltaDto,
        OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> GetMapSequentialIntKeys(
        int nKeys, int nElements, int nActors, bool addPopulated = true, int startKeysAt = 0)
    {
        
        var map = NewMap();
        var actors = Enumerable.Range(0, nActors).Select(_ => Guid.NewGuid()).ToList();

        for (var i = startKeysAt; i < startKeysAt + nKeys; ++i)
        {
            if (addPopulated)
            {
                var set = new OptimizedObservedRemoveSetV3<Guid, double>();
                for (var j = 0; j < nElements; ++j)
                {
                    set.Add(_random.NextDouble(), actors[(i * nElements + j) % actors.Count]);
                }

                map.AddOrMerge(actors[i % actors.Count], i, set);
            }
            else
            {
                map.AddOrMerge(actors[i % actors.Count], i, new OptimizedObservedRemoveSetV3<Guid, double>());

                for (var j = 0; j < nElements; ++j)
                {
                    var i1 = i;
                    var j1 = j;
                    map.TryMutate(actors[i % actors.Count], 
                        i, 
                        set => set.Add(_random.NextDouble(), actors[(i1 * nElements + j1) % actors.Count]), 
                        out _);
                }
            }
        }

        return map;
    }

    private void AssertMapEquality<TActorId, TKey, TValue>(
        ObservedRemoveMapV2<TActorId, 
            TKey, 
            OptimizedObservedRemoveSetV3<TActorId, TValue>, 
            OptimizedObservedRemoveCore<TActorId, TValue>.DeltaDto, 
            OptimizedObservedRemoveCore<TActorId, TValue>.CausalTimestamp> map1,
        ObservedRemoveMapV2<TActorId, 
            TKey, 
            OptimizedObservedRemoveSetV3<TActorId, TValue>, 
            OptimizedObservedRemoveCore<TActorId, TValue>.DeltaDto, 
            OptimizedObservedRemoveCore<TActorId, TValue>.CausalTimestamp> map2)
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>
    {
        map1.Count.Should().Be(map2.Count);
        map1.Keys.ToHashSet().SetEquals(map2.Keys).Should().BeTrue();

        foreach (var key in map1.Keys)
        {
            map1.TryGet(key, out var values1).Should().BeTrue();
            map2.TryGet(key, out var values2).Should().BeTrue();

            try
            {
                AssertSetEquality(values1!, values2!);
            }
            catch (XunitException)
            {
                _output.WriteLine($"Sets for key {key} differ in produced dtos");
                throw;
            }
        }

        AssertDeltasEquality(map1, map2);
    }

    private static void AssertSetEquality<TActor, TItem>(OptimizedObservedRemoveSetV3<TActor, TItem> set1,
        OptimizedObservedRemoveSetV3<TActor, TItem> set2)
        where TItem : IEquatable<TItem>
        where TActor : IEquatable<TActor>, IComparable<TActor>
    {
        set1.Values.ToHashSet().SetEquals(set2.Values).Should().BeTrue();
        var deltas1 = set1.EnumerateDeltaDtos().ToHashSet();
        var deltas2 = set2.EnumerateDeltaDtos().ToHashSet();
        deltas1.SetEquals(deltas2).Should().BeTrue();
        // set1.ToDto().Should().BeEquivalentTo(set2.ToDto());
    }
    
    private static void AssertDeltasEquality<TDelta, TTimestamp>(IDeltaCrdt<TDelta, TTimestamp> crdt1,
        IDeltaCrdt<TDelta, TTimestamp> crdt2)
    {
        var deltas1 = crdt1.EnumerateDeltaDtos().ToHashSet();
        var deltas2 = crdt2.EnumerateDeltaDtos().ToHashSet();

        deltas1.Should().HaveSameCount(deltas2);
        // deltas1.SetEquals(deltas2).Should().BeTrue();
        deltas1.Should().BeEquivalentTo(deltas2, options => options
            .ComparingRecordsByMembers()
            .WithoutStrictOrdering());
    }
    
    private async Task AddAndRemoveContinuouslyAsync(ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map,
        IReadOnlyCollection<int> keys,
        Guid actorId,
        TimeSpan duration,
        TimeSpan pauseLength)
    {
        var start = DateTime.Now;
        var counter = 0;
        while (DateTime.Now - duration < start)
        {
            foreach (var key in keys)
            {
                MakeRandomOperation(map, actorId, key);
            }
            
            await Task.Delay(pauseLength);
            ++counter;
        }

        var avg = (DateTime.Now - start) / counter - pauseLength;
        _output.WriteLine($"Repeated AddRemoveMutate of {keys.Count} key-value pairs finished, {counter} cycles " +
                          $"executed, average duration is {avg}");
    }
    
    private async Task DeltaMergeContinuouslyAsync(ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map1,
        ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map2,
        TimeSpan duration,
        TimeSpan pauseLength)
    {
        var start = DateTime.Now;
        var counter = 0;
        while (DateTime.Now - duration < start)
        {
            await Task.Delay(pauseLength);
            DeltaMerge(map1, map2, false, true, true, true);
            ++counter;
        }
        
        var avg = (DateTime.Now - start) / counter - pauseLength;
        _output.WriteLine($"Repeated delta merges is finished, {counter} cycles executed," +
                          $" average 2-sided merge duration is {avg}");
    }
    
    private void DeltaMerge(ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map1, 
        ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp> map2,
        bool log = false,
        bool drops = false, 
        bool duplicates = true, 
        bool reordering = true)
    {
        var delayedMessagesTo1 = new List<ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp>.DeltaDto>();
        var delayedMessagesTo2 = new List<ObservedRemoveMapV2<Guid, 
            int, 
            OptimizedObservedRemoveSetV3<Guid, double>, 
            OptimizedObservedRemoveCore<Guid, double>.DeltaDto, 
            OptimizedObservedRemoveCore<Guid, double>.CausalTimestamp>.DeltaDto>();
        
        var timestamp = map2.GetLastKnownTimestamp();
        foreach (var dto in map1.EnumerateDeltaDtos(timestamp))
        {
            switch (_random.NextDouble())
            {
                case < 0.1:   // message dropped
                    if (!drops) map2.Merge(dto);
                    break;
                case < 0.2:   // message duplicate
                    map2.Merge(dto);
                    if (duplicates) map2.Merge(dto);
                    break;
                case < 0.8:   // message delayed
                    if(reordering) delayedMessagesTo2.Add(dto);
                    else map2.Merge(dto);
                    break;
                default:
                    map2.Merge(dto);
                    break;
            }
        }
        timestamp = map1.GetLastKnownTimestamp();
        foreach (var dto in map2.EnumerateDeltaDtos(timestamp))
        {
            switch (_random.NextDouble())
            {
                case < 0.1:   // message dropped
                    if (!drops) map1.Merge(dto);
                    break;
                case < 0.2:   // message duplicate
                    map1.Merge(dto);
                    if (duplicates) map1.Merge(dto);
                    break;
                case < 0.4:   // message delayed
                    if(reordering) delayedMessagesTo1.Add(dto);
                    else map1.Merge(dto);
                    break;
                default:
                    map1.Merge(dto);
                    break;
            }
        }

        _random.Shuffle(delayedMessagesTo1);
        _random.Shuffle(delayedMessagesTo2);

        foreach (var dto in delayedMessagesTo1)
        {
            map1.Merge(dto);
        }
        foreach (var dto in delayedMessagesTo2)
        {
            map2.Merge(dto);
        }
    }
}