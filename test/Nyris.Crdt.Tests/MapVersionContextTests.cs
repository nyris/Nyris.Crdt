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

        context.ObserveAndClear(new Range(6, 7));
        context.GetEmptyRanges().Should().HaveCount(3);

        var except = ImmutableArray.Create(new Range(1, 2), new Range(5, 6));
        var keys = new List<int>();
        foreach (var key in context.EnumerateKeysOutsideRanges(except)) 
        {
            keys.Add(key);
        }

        keys.Should().BeEquivalentTo(new[] {1, 2, 3});
    }
}