using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Sets;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.Tests;

public sealed class ObservedRemoveSetV3Tests
{
    private readonly ITestOutputHelper _output;
    private readonly Random _random = new(42);

    public ObservedRemoveSetV3Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(10, 3)]
    [InlineData(1000, 100)]
    public void AddWorks(int nElement, int nActors)
    {
        var elements = GetRandomDoubles(nElement);
        var set = GetSetWith(nActors, elements);

        AssertEquality(set, elements);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(10, 1, 5)]
    [InlineData(10, 3, 5)]
    [InlineData(1000, 100, 500)]
    public void RemoveWorks(int nElement, int nActors, int nRemoves)
    {
        var set = GetSetWithRemovals(nElement, nActors, nRemoves, out var elementsSet);
        AssertEquality(set, elementsSet);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(10, 3)]
    [InlineData(1000, 100)]
    public void AddThenMergeWorks(int nElement, int nActors)
    {
        var elements = GetRandomDoubles(nElement);
        var set1 = GetSetWith(nActors, elements);
        var set2 = NewSet();

        MergeToRight(set1, set2);
        AssertEquality(set2, elements);
        AssertEquality(set1, set2);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(10, 1, 5)]
    [InlineData(10, 3, 5)]
    [InlineData(1000, 100, 500)]
    public void AddRemoveThenMergeWorks(int nElement, int nActors, int nRemoves)
    {
        var set1 = GetSetWithRemovals(nElement, nActors, nRemoves, out var elementsSet);
        var set2 = NewSet();

        MergeToRight(set1, set2);
        AssertEquality(set2, elementsSet);
        AssertEquality(set1, set2);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(10, 1, 5)]
    [InlineData(10, 3, 5)]
    [InlineData(1000, 100, 500)]
    public void ParallelAddThenMergeWorks(int nElement, int nActors, int nRemoves)
    {
        var set1 = GetSetWithRemovals(nElement, nActors, nRemoves, out _);
        var set2 = NewSet();
        MergeToRight(set1, set2);

        set1.Add(_random.NextDouble(), 0);
        set2.Add(_random.NextDouble(), 1);

        Merge(set1, set2);
        AssertEquality(set1, set2);
    }

    [Theory]
    [InlineData(2, 1, 0)]
    [InlineData(10, 1, 5)]
    [InlineData(10, 3, 5)]
    [InlineData(1000, 100, 500)]
    public void ParallelRemoveThenMergeWorks(int nElement, int nActors, int nRemoves)
    {
        var set1 = GetSetWithRemovals(nElement, nActors, nRemoves, out var elements);
        var set2 = NewSet();
        MergeToRight(set1, set2);

        RemoveFromSets(set1, elements, 1);
        RemoveFromSets(set2, elements, 1);

        Merge(set1, set2);
        AssertEquality(set1, elements);
        AssertEquality(set1, set2);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(10, 1)]
    [InlineData(10, 3)]
    [InlineData(1000, 10)]
    public void SequentialAddRemoveMergeWorks(int nSteps, int nActors)
    {
        var actors = GetActors(nActors);
        var referenceSet = new HashSet<double>();
        var set1 = NewSet();
        var set2 = NewSet();

        for (var i = 0; i < nSteps; ++i)
        {
            switch (_random.NextDouble(), set1.Count)
            {
                case (< 0.5, > 0):
                    RemoveFromSets(set1, referenceSet, 1);
                    break;
                default:
                    AddToSets(set1, referenceSet, actors);
                    break;
            }
            MergeToRight(set1, set2);
            AssertEquality(set2, referenceSet);
            AssertEquality(set1, set2);
        }
    }

    [Fact]
    public void SetObserverWorks()
    {
        // prepare
        var observer = new DummySetObserver();
        var set1 = NewSet();
        var set2 = NewSet();
        set1.SubscribeToChanges(observer);

        // act
        var expectedSet = new HashSet<double>();
        AddToSets(set2, expectedSet, new[] {1, 2}, 100);
        MergeToRight(set2, set1);  // observer notified about 'remote' additions

        RemoveFromSets(set2, expectedSet, expectedSet.Count / 2);
        MergeToRight(set2, set1);  // observer notified about 'remote' removal

        AddToSets(set1, expectedSet, new[] {3, 4}, 100); // observer notified about local additions
        RemoveFromSets(set1, expectedSet, expectedSet.Count / 2); // observer notified about local removals

        // assert
        AssertEquality(set1, expectedSet);
        AssertEquality(set1, observer.Values);
    }

    [Fact]
    public void ConcurrentAddRemoveMergeWorks()
    {
        // prepare
        var set1 = GetSetWithRemovals(100, 4, 50, out _);
        var set2 = NewSet();
        MergeToRight(set1, set2);

        // act
        var thread1 = new Thread(() => OperateOnSetInLoop(set1, 0, TimeSpan.FromSeconds(1)));
        var thread2 = new Thread(() => OperateOnSetInLoop(set2, 1, TimeSpan.FromSeconds(1)));
        var thread3 = new Thread(() => MergeSetsInLoop(set1, set2, TimeSpan.FromSeconds(1)));
        StartThreadsAndWait(thread1, thread2, thread3);

        // assert
        Merge(set1, set2);
        AssertEquality(set1, set2);
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
        IDeltaCrdt<ObservedRemoveCore<int, double>.DeltaDto,
            ObservedRemoveCore<int, double>.CausalTimestamp> set1,
        IDeltaCrdt<ObservedRemoveCore<int, double>.DeltaDto,
            ObservedRemoveCore<int, double>.CausalTimestamp> set2, TimeSpan duration)
    {
        var start = DateTime.Now;
        while (DateTime.Now - start < duration)
        {
            MergeToRight(set1, set2, true, true, true);
            MergeToRight(set2, set1, true, true, true);
        }
    }

    private void OperateOnSetInLoop(ObservedRemoveSetV3<int, double> set, int actor, TimeSpan duration)
    {
        var start = DateTime.Now;
        while (DateTime.Now - start < duration)
        {
            switch (_random.NextDouble(), set.Count)
            {
                case (< 0.5, > 0):
                    var toRemove = set.Values.First();
                    set.Remove(toRemove);
                    break;
                default:
                    set.Add(_random.NextDouble(), actor);
                    break;
            }
        }
    }

    private void AddToSets(ObservedRemoveSetV3<int, double> set, HashSet<double> referenceSet, int[] actors,
        int count = 1)
    {
        for (var i = 0; i < count; ++i)
        {
            var value = _random.NextDouble();
            referenceSet.Add(value);
            set.Add(value, actors[_random.Next(0, actors.Length)]);
        }
    }

    private ObservedRemoveSetV3<int, double> GetSetWithRemovals(int nElement, int nActors, int nRemoves,
        out HashSet<double> elementsSet)
    {
        var elements = GetRandomDoubles(nElement);
        var set = GetSetWith(nActors, elements);
        elementsSet = elements.ToHashSet();
        RemoveFromSets(set, elementsSet, nRemoves);
        return set;
    }
    private void Merge(IDeltaCrdt<ObservedRemoveCore<int, double>.DeltaDto, ObservedRemoveCore<int, double>.CausalTimestamp> set1,
        IDeltaCrdt<ObservedRemoveCore<int, double>.DeltaDto, ObservedRemoveCore<int, double>.CausalTimestamp> set2)
    {
        MergeToRight(set1, set2);
        MergeToRight(set2, set1);
    }


    private void MergeToRight(IDeltaCrdt<ObservedRemoveCore<int, double>.DeltaDto, ObservedRemoveCore<int, double>.CausalTimestamp> left,
        IDeltaCrdt<ObservedRemoveCore<int, double>.DeltaDto, ObservedRemoveCore<int, double>.CausalTimestamp> right,
        bool skip = false,
        bool reorder = false,
        bool duplicate = false)
    {
        var lateDeltas = new List<ObservedRemoveCore<int, double>.DeltaDto>();
        foreach (var delta in left.EnumerateDeltaDtos(right.GetLastKnownTimestamp()))
        {
            switch (_random.NextDouble())
            {
                case < 0.25:
                    if(!skip) right.Merge(delta);
                    break;
                case < 0.5:
                    right.Merge(delta);
                    if(duplicate) right.Merge(delta);
                    break;
                case < 0.75:
                    if (reorder) lateDeltas.Add(delta);
                    else right.Merge(delta);
                    break;
                default:
                    right.Merge(delta);
                    break;
            }
        }

        foreach (var delta in lateDeltas)
        {
            right.Merge(delta);
        }
    }

    private static void RemoveFromSets(ObservedRemoveSetV3<int, double> set, HashSet<double> elementsSet, int nRemoves)
    {
        for (var i = 0; i < nRemoves; ++i)
        {
            var toRemove = elementsSet.First();
            set.Remove(toRemove);
            elementsSet.Remove(toRemove);
        }
    }

    private static void AssertDeltasEquality<TDelta, TTimestamp>(IDeltaCrdt<TDelta, TTimestamp> crdt1,
        IDeltaCrdt<TDelta, TTimestamp> crdt2)
    {
        var deltas1 = crdt1.EnumerateDeltaDtos().ToArray();
        var deltas2 = crdt2.EnumerateDeltaDtos().ToArray();

        deltas1.Should().HaveSameCount(deltas2);
        deltas1.Should().BeEquivalentTo(deltas2, options => options
            .ComparingRecordsByMembers()
            .WithoutStrictOrdering());
    }

    private static void AssertEquality(ObservedRemoveSetV3<int, double> set1, ObservedRemoveSetV3<int, double> set2)
    {
        set1.Values.ToHashSet().SetEquals(set2.Values).Should().BeTrue();
        AssertDeltasEquality(set1, set2);
    }

    private static void AssertEquality(ObservedRemoveSetV3<int, double> set, IEnumerable<double> elements)
    {
        set.Values.ToHashSet().SetEquals(elements).Should().BeTrue("we expect to find elements we added");
    }

    private static int[] GetActors(int nActors)
    {
        var actors = new int[nActors];
        for (var i = 0; i < nActors; ++i)
        {
            actors[i] = i;
        }

        return actors;
    }

    public static ObservedRemoveSetV3<int, double> NewSet() => new();

    private ObservedRemoveSetV3<int, double> GetSetWith(int nActors, double[] elements)
    {
        var set = NewSet();
        var actors = GetActors(nActors);
        for (var i = 0; i < elements.Length; ++i)
        {
            set.Add(elements[i], actors[_random.Next(0, actors.Length)]);
        }

        return set;
    }

    private double[] GetRandomDoubles(int nElement)
    {
        var elements = new double[nElement];
        for (var i = 0; i < nElement; ++i)
        {
            elements[i] = _random.NextDouble();
        }

        return elements;
    }


    public sealed class DummySetObserver : ISetObserver<double>
    {
        public HashSet<double> Values { get; } = new();

        public void ElementAdded(double item) => Values.Add(item);
        public void ElementRemoved(double item) => Values.Remove(item);
    }
}