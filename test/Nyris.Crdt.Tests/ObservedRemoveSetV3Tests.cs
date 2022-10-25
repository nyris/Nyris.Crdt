using System;
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
    public void AddWorks(int nElement, int nActors)
    {
        var set = NewSet();
        var actors = GetActors(nActors);

        for (var i = 0; i < nElement; ++i)
        {
            set.Add(_random.NextDouble(), actors[_random.Next(0, actors.Length)]);
        }
        
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

    public static OptimizedObservedRemoveSetV3<int, double> NewSet() => new();
}