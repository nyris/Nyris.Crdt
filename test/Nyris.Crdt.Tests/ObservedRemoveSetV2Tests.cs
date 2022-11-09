using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nyris.Crdt.Sets;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.Tests;

public sealed class ObservedRemoveSetV2Tests
{
    private readonly ITestOutputHelper _output;
    private readonly Random _random = new(42);

    public ObservedRemoveSetV2Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(100, 0, 1)]
    [InlineData(11, 0, 7)]
    [InlineData(127, 0, 11)]
    [InlineData(127, 0, 47)]
    [InlineData(10, 5, 1)]
    [InlineData(127, 23, 1)]
    [InlineData(127, 47, 1)]
    [InlineData(127, 113, 1)]
    [InlineData(127, 23, 3)]
    [InlineData(127, 47, 3)]
    [InlineData(127, 113, 3)]
    [InlineData(10, 5, 10)]
    [InlineData(127, 47, 11)]
    [InlineData(127, 59, 47)]
    [InlineData(10000, 3500, 99)]
    [InlineData(1, 0, 1, false)]
    [InlineData(2, 0, 1, false)]
    [InlineData(2, 2, 1, false)]
    [InlineData(10, 0, 1, false)]
    [InlineData(100, 0, 1, false)]
    [InlineData(11, 0, 7, false)]
    [InlineData(127, 0, 11, false)]
    [InlineData(127, 0, 47, false)]
    [InlineData(10, 5, 1, false)]
    [InlineData(127, 23, 1, false)]
    [InlineData(127, 47, 1, false)]
    [InlineData(127, 113, 1, false)]
    [InlineData(127, 23, 3, false)]
    [InlineData(127, 47, 3, false)]
    [InlineData(127, 113, 3, false)]
    [InlineData(10, 5, 10, false)]
    [InlineData(127, 47, 11, false)]
    [InlineData(127, 59, 47, false)]
    [InlineData(10000, 3500, 99, false)]
    public void MergeIntoEmpty_Works(int nElements, int nDeletes, int nActors, bool deltaMerge = true)
    {
        var set1 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var actors = Enumerable.Range(0, nActors).Select(_ => Guid.NewGuid()).ToList();
        var expectedValues = new Dictionary<int, double>(Enumerable
            .Range(0, nElements)
            .Select(i => new KeyValuePair<int, double>(i, _random.NextDouble())));
        
        var start = DateTime.Now;
        for (var i = 0; i < nElements; ++i)
        {
            set1.Add(expectedValues[i], actors[i % actors.Count]);
        }
        _output.WriteLine($"Adds done in {DateTime.Now - start}");

        start = DateTime.Now;
        for (var i = 0; i < nDeletes; ++i)
        {
            var indexToDelete = _random.Next(0, nElements);
            while (!expectedValues.ContainsKey(indexToDelete))
            {
                indexToDelete = indexToDelete > 0 ? indexToDelete - 1 : nElements - 1;
            }
            var valueToDelete = expectedValues[indexToDelete];
            
            set1.Remove(valueToDelete);
            expectedValues.Remove(indexToDelete);
        }
        _output.WriteLine($"Removes done in {DateTime.Now - start}");

        var set2 = new OptimizedObservedRemoveSetV2<Guid, double>();
        start = DateTime.Now;
        if(deltaMerge) DeltaMerge(set1, set2);
        else Merge(set1, set2);
        _output.WriteLine($"Merge done in {DateTime.Now - start}");

        set2.Values.SetEquals(expectedValues.Values).Should().BeTrue();
        AssertSetEquality(set1, set2);
    }

    [Theory]
    [InlineData(11, 0)]
    [InlineData(127, 0)]
    [InlineData(10, 5)]
    [InlineData(127, 23)]
    [InlineData(127, 47)]
    [InlineData(127, 113)]
    [InlineData(20000, 7000)]
    [InlineData(11, 0, false)]
    [InlineData(127, 0, false)]
    [InlineData(10, 5, false)]
    [InlineData(127, 23, false)]
    [InlineData(127, 47, false)]
    [InlineData(127, 113, false)]
    [InlineData(20000, 7000, false)]
    public void EqualSplitDistinctActors_Works(int nElements, int nDeletes, bool deltaMerge = true)
    {
        var set1 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var set2 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var actor1 = Guid.NewGuid();
        var actor2 = Guid.NewGuid();
        var expectedValues = new Dictionary<int, double>(Enumerable
            .Range(0, nElements)
            .Select(i => new KeyValuePair<int, double>(i, _random.NextDouble())));

        var start = DateTime.Now;
        foreach (var (i, value) in expectedValues)
        {
            if(i % 2 == 0) set1.Add(value, actor1);
            else set2.Add(value, actor2);
        }
        _output.WriteLine($"Adds done in {DateTime.Now - start}");

        start = DateTime.Now;
        for (var i = 0; i < nDeletes; ++i)
        {
            var indexToDelete = _random.Next(0, nElements);
            while (!expectedValues.ContainsKey(indexToDelete))
            {
                indexToDelete = indexToDelete > 0 ? indexToDelete - 1 : nElements - 1;
            }
            var valueToDelete = expectedValues[indexToDelete];
            
            if(set1.Values.Contains(valueToDelete)) set1.Remove(valueToDelete);
            else set2.Remove(valueToDelete);
            
            expectedValues.Remove(indexToDelete);
        }
        _output.WriteLine($"Deletes done in {DateTime.Now - start}");

        start = DateTime.Now;
        if(deltaMerge) DeltaMerge(set1, set2);
        else Merge(set1, set2);
        _output.WriteLine($"Merge done in {DateTime.Now - start}");

        start = DateTime.Now;
        set1.Values.SetEquals(expectedValues.Values).Should().BeTrue(
            $"expected {expectedValues.Count} items, but this were not found: " +
            $"{string.Join(";", expectedValues.Values.Except(set1.Values))}");
        
        AssertSetEquality(set1, set2);
        _output.WriteLine($"Assertions done in {DateTime.Now - start}");
    }

    
    [Theory]
    [InlineData(1, 100)]
    [InlineData(3, 100)]
    [InlineData(5, 100)]
    [InlineData(11, 100)]
    [InlineData(100, 100)]
    [InlineData(1, 1000)]
    [InlineData(3, 1000)]
    [InlineData(5, 1000)]
    [InlineData(11, 1000)]
    [InlineData(100, 1000)]
    [InlineData(1, 100, false)]
    [InlineData(3, 100, false)]
    [InlineData(5, 100, false)]
    [InlineData(11, 100, false)]
    [InlineData(100, 100, false)]
    [InlineData(1, 1000, false)]
    [InlineData(3, 1000, false)]
    [InlineData(5, 1000, false)]
    [InlineData(11, 1000, false)]
    [InlineData(100, 1000, false)]
    public void RepeatedAddRemoveMerge_Works(int nActors, int nOperations, bool deltaMerge = true)
    {
        var set1 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var set2 = new OptimizedObservedRemoveSetV2<Guid, double>();
        var actors = Enumerable.Range(0, nActors).Select(_ => Guid.NewGuid()).ToList();

        set1.Add(1.0, actors[0]);
        if(deltaMerge) DeltaMerge(set1, set2);
        else Merge(set1, set2);
        
        for (var i = 0; i < nOperations; ++i)
        {
            switch (_random.NextDouble())
            {
                case < 0.3:
                    set1.Add(_random.NextDouble(), RandomActor());
                    break;
                case < 0.6:
                    set2.Add(_random.NextDouble(), RandomActor());
                    break;
                case < 0.8:
                    set1.Remove(set1.Values.FirstOrDefault());
                    break;
                default:
                    set2.Remove(set2.Values.FirstOrDefault());
                    break;
            }
            if(deltaMerge) DeltaMerge(set1, set2);
            else Merge(set1, set2);
        }
        
        set1.Values.SetEquals(set2.Values).Should().BeTrue();
        AssertSetEquality(set1, set2);

        Guid RandomActor() => actors![_random.Next(0, actors.Count)];
    }

    [Theory]
    [InlineData(3, 100)]
    [InlineData(6, 100)]
    [InlineData(10, 100)]
    [InlineData(3, 1000)]
    [InlineData(6, 1000)]
    [InlineData(10, 1000)]
    [InlineData(3, 10000)]
    [InlineData(6, 10000)]
    [InlineData(10, 10000)]
    [InlineData(23, 10000)]
    [InlineData(3, 100, false)]
    [InlineData(6, 100, false)]
    [InlineData(10, 100, false)]
    [InlineData(3, 1000, false)]
    [InlineData(6, 1000, false)]
    [InlineData(10, 1000, false)]
    [InlineData(3, 10000, false)]
    [InlineData(6, 10000, false)]
    [InlineData(10, 10000, false)]
    [InlineData(23, 10000, false)]
    public void ConcurrentMerge_Works(int nSets, int nElements, bool deltaMerge = true)
    {
        var sets = Enumerable
            .Range(0, nSets)
            .Select(_ => new OptimizedObservedRemoveSetV2<Guid, double>())
            .ToList();

        var actors = sets.Select(_ => Guid.NewGuid()).ToList();
        var expectedValues = Enumerable.Range(0, nElements).Select(_ => _random.NextDouble()).ToList();
        
        for (var i = 0; i < sets.Count; ++i)
        {
            // first add all values to all sets
            foreach (var value in expectedValues)
            {
                sets[i].Add(value, actors[i]);
            }

            // then each set removes a subset of values, non-intersecting with other sets
            foreach (var value in expectedValues.Where((_, k) => k % sets.Count == i))
            {
                sets[i].Remove(value);
            }
        }

        Parallel.For(1, sets.Count, i =>
        {
            if(deltaMerge) DeltaMerge(sets[i], sets[0]);
            else Merge(sets[i], sets[0]);
        });

        sets[0].Values.SetEquals(expectedValues).Should().BeTrue();
        
        // merge second time to propagate updates to all sets, not just 0-th 
        Parallel.For(1, sets.Count, i =>
        {
            if(deltaMerge) DeltaMerge(sets[i], sets[0]);
            else Merge(sets[i], sets[0]);
        });

        foreach (var set in sets)
        {
            set.Values.SetEquals(expectedValues).Should().BeTrue(
                $"expected {expectedValues.Count} items, but this were not found: " +
                $"{string.Join(";", expectedValues.Except(set.Values))}");
        }

        for (var i = 1; i < sets.Count; ++i)
        {
            AssertSetEquality(sets[0], sets[i]);
        }
    }

    [Theory]
    [InlineData(18, 4, false)]
    [InlineData(100, 2, false)]
    [InlineData(100, 3, false)]
    [InlineData(100, 10, false)]
    [InlineData(1000, 2, false)]
    [InlineData(1000, 3, false)]
    [InlineData(1000, 10, false)]
    [InlineData(10000, 2, false)]
    [InlineData(10000, 3, false)]
    [InlineData(10000, 10, false)]
    [InlineData(18, 4)]
    [InlineData(100, 2)]
    [InlineData(100, 3)]
    [InlineData(100, 10)]
    [InlineData(1000, 2)]
    [InlineData(1000, 3)]
    [InlineData(1000, 10)]
    [InlineData(10000, 2)]
    [InlineData(10000, 3)]
    [InlineData(10000, 10)]
    public async Task DeltaMerging_ConcurrentAddsRemovesAndMerges_Works(int nElements, int nSets, bool deletes = true)
    {
        var sets = Enumerable
            .Range(0, nSets)
            .Select(_ => new OptimizedObservedRemoveSetV2<Guid, double>())
            .ToList();

        var actors = sets.Select(_ => Guid.NewGuid()).ToList();
        var expectedValues = Enumerable.Range(0, nElements).Select(_ => _random.NextDouble()).ToList();
            
        var tasks = new Task[sets.Count * 2];
        var start = DateTime.Now;
        for (var i = 0; i < sets.Count; ++i)
        {
            var next = i > 0 ? i - 1 : sets.Count - 1;
            
            // Distribute all elements between actors (include element if [element index] % [n actors] == [actor index])
            // But also add some other elements where that condition is not met, so that there is overlap
            var actorsElements = expectedValues
                .Where((_, j) => j % nSets == i || _random.NextDouble() < 0.2)
                .ToList();
            
            tasks[i] = DeltaMergeContinuouslyAsync(sets[i], sets[next], 
                TimeSpan.FromMilliseconds(800),
                TimeSpan.FromMilliseconds(10));

            if (actorsElements.Count < 20)
            {
                _output.WriteLine("Starting an add-remove task with elements: {0}", string.Join(", ", actorsElements));
            }
            tasks[sets.Count + i] = AddAndRemoveContinuouslyAsync(sets[i], actorsElements, actors[i],
                TimeSpan.FromMilliseconds(800), 
                TimeSpan.FromMilliseconds(10), 
                deletes);
        }

        await Task.WhenAll(tasks);
        _output.WriteLine($"All tasks awaited in {DateTime.Now - start}");
        
        start = DateTime.Now;
        for (var i = 0; i < sets.Count; ++i)
        for (var j = i + 1; j < sets.Count; ++j)
        {
            DeltaMerge(sets[i], sets[j]);
        }
        _output.WriteLine($"All sets merged in {DateTime.Now - start}");

        foreach (var set in sets)
        {
            set.Values.SetEquals(expectedValues).Should().BeTrue(
                $"expected {expectedValues.Count} items, but this were not found: " +
                $"{string.Join(";", expectedValues.Except(set.Values))}");
        }
        for (var i = 1; i < sets.Count; ++i)
        {
            AssertSetEquality(sets[0], sets[i]);
        }
    }

    private static void AssertSetEquality<TActor, TItem>(OptimizedObservedRemoveSetV2<TActor, TItem> set1,
        OptimizedObservedRemoveSetV2<TActor, TItem> set2)
        where TItem : IEquatable<TItem>
        where TActor : IEquatable<TActor>, IComparable<TActor>
    {
        set1.Values.SetEquals(set2.Values).Should().BeTrue();
        var deltas1 = set1.EnumerateDeltaDtos().ToHashSet();
        var deltas2 = set2.EnumerateDeltaDtos().ToHashSet();
        deltas1.SetEquals(deltas2).Should().BeTrue();
        set1.ToDto().Should().BeEquivalentTo(set2.ToDto());
    }

    private async Task AddAndRemoveContinuouslyAsync<TActorId, TItem>(OptimizedObservedRemoveSetV2<TActorId, TItem> set,
        IReadOnlyCollection<TItem> items,
        TActorId actorId,
        TimeSpan duration,
        TimeSpan pauseLength,
        bool removes = true)
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
        where TItem : IEquatable<TItem>
    {
        var start = DateTime.Now;
        var counter = 0;
        while (DateTime.Now - duration < start)
        {
            if (removes)
            {
                var currentValues = set.Values;
                var nDelete = currentValues.Count / 3;
                var itemsToRemove = currentValues.Take(nDelete).ToList();
                foreach (var item in itemsToRemove)
                {
                    set.Remove(item);
                }

                await Task.Delay(pauseLength);

                foreach (var item in itemsToRemove)
                {
                    set.Add(item, actorId);
                }
            }

            foreach (var item in items)
            {
                set.Add(item, actorId);
            }

            await Task.Delay(pauseLength);
            ++counter;
        }

        var avg = (DateTime.Now - start) / counter - pauseLength * (removes ? 2 : 1);
        _output.WriteLine($"Repeated AddRemove of {items.Count} items finished, {counter} cycles executed," +
                          $" average duration is {avg}");
    }
    
    private async Task DeltaMergeContinuouslyAsync<TActorId, TItem>(OptimizedObservedRemoveSetV2<TActorId, TItem> set1,
        OptimizedObservedRemoveSetV2<TActorId, TItem> set2,
        TimeSpan duration,
        TimeSpan pauseLength)
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
        where TItem : IEquatable<TItem>
    {
        var start = DateTime.Now;
        var counter = 0;
        while (DateTime.Now - duration < start)
        {
            await Task.Delay(pauseLength);
            DeltaMerge(set1, set2, true);
            ++counter;
        }
        
        var avg = (DateTime.Now - start) / counter - pauseLength;
        _output.WriteLine($"Repeated delta merges is finished, {counter} cycles executed," +
                          $" average 2-sided merge duration is {avg}");
    }

    private void Merge<TActorId, TItem>(OptimizedObservedRemoveSetV2<TActorId, TItem> set1,
        OptimizedObservedRemoveSetV2<TActorId, TItem> set2)
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
        where TItem : IEquatable<TItem>
    {
        var dto1 = set1.ToDto();
        set1.Merge(set2.ToDto());
        set2.Merge(dto1);
    }
    
    private void DeltaMerge<TActorId, TItem>(OptimizedObservedRemoveSetV2<TActorId, TItem> set1,
        OptimizedObservedRemoveSetV2<TActorId, TItem> set2,
        bool networkProblems = false)
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>
        where TItem : IEquatable<TItem>
    {
        var delayedMessagesTo1 = new List<ObservedRemoveDtos<TActorId, TItem>.DeltaDto>();
        var delayedMessagesTo2 = new List<ObservedRemoveDtos<TActorId, TItem>.DeltaDto>();
        foreach (var dto in set1.EnumerateDeltaDtos(set2.GetLastKnownTimestamp()))
        {
            if (!networkProblems)
            {
                set2.Merge(dto);
                continue;
            }

            switch (_random.NextDouble())
            {
                case < 0.1:   // message dropped
                    break;
                case < 0.2:   // message duplicate
                    set2.Merge(dto);
                    set2.Merge(dto);
                    break;
                case < 0.4:   // message delayed
                    delayedMessagesTo2.Add(dto);
                    break;
                default:
                    set2.Merge(dto);
                    break;
            }
        }
        foreach (var dto in set2.EnumerateDeltaDtos(set1.GetLastKnownTimestamp()))
        {
            if (!networkProblems)
            {
                set1.Merge(dto);
                continue;
            }

            switch (_random.NextDouble())
            {
                case < 0.1:   // message dropped
                    break;
                case < 0.2:   // message duplicate
                    set1.Merge(dto);
                    set1.Merge(dto);
                    break;
                case < 0.4:   // message delayed
                    delayedMessagesTo1.Add(dto);
                    break;
                default:
                    set1.Merge(dto);
                    break;
            }
        }

        _random.Shuffle(delayedMessagesTo1);
        _random.Shuffle(delayedMessagesTo2);
        
        foreach (var dto in delayedMessagesTo1)
        {
            set1.Merge(dto);
        }
        foreach (var dto in delayedMessagesTo2)
        {
            set2.Merge(dto);
        }
    }
}