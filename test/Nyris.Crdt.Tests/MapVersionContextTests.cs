using System.Collections.Generic;
using System.Collections.Immutable;
using FluentAssertions;
using Nyris.Crdt.Model;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class MapVersionContextTests
{
    [Fact]
    public void Test()
    {
        var context = new MapVersionContext<int>();

        var v1 = context.GetNewVersion(1);
        context.GetNewVersion(2);

        var v3 = context.GetNewVersion(1);
        context.MaybeClearVersion(v1);

        context.GetNewVersion(3);
        context.GetNewVersion(1);

        context.MaybeClearVersion(v3);

        context.GetEmptyRanges().Should().HaveCount(2);

        context.TryInsert(1, 7).Should().BeTrue();
        context.GetEmptyRanges().Should().HaveCount(2);

        context.ObserveAndClear(new Range(6, 7), out _);
        context.GetEmptyRanges().Should().HaveCount(3);

        var except = ImmutableArray.Create(new Range(1, 2), new Range(5, 6));
        var keys = new List<int>();
        foreach (var key in context.EnumerateKeysOutsideRanges(except))
        {
            keys.Add(key);
        }

        keys.Should().BeEquivalentTo(new[] {1, 2, 3});
    }

    [Fact]
    public void Test2()
    {
        var context = new MapVersionContext<int>();

        context.TryInsert(-1, 1);
        context.TryInsert(-2, 2);
        context.TryInsert(-3, 3);
        context.TryInsert(-4, 4);
        context.TryInsert(-4, 5);

        context.ObserveAndClear(new Range(1, 3), out _);
        context.ObserveAndClear(new Range(4, 5), out _);

        var ranges = context.GetEmptyRanges();
        ranges.Should().HaveCount(2)
            .And.ContainSingle(r => r == new Range(1, 3))
            .And.ContainSingle(r => r == new Range(4, 5));
    }
}