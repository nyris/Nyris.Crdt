using System;
using System.Threading.Tasks;
using FluentAssertions;
using Nyris.Crdt.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.Tests;

public sealed class VersionContextTests
{
    private readonly ITestOutputHelper _output;

    public VersionContextTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ConcurrentIncrementsWork()
    {
        var vv = new VersionContext<Guid>();
        var actorId = Guid.NewGuid();

        Parallel.For(0, 1000, _ => vv.Increment(actorId));
        vv.Increment(actorId).Should().Be(1001);
    }

    [Fact]
    public void ConcurrentMergesWork()
    {
        var vv = new VersionContext<Guid>();
        var actorId = Guid.NewGuid();

        Parallel.For(0, 1000, i =>
        {
            vv.Merge(actorId, (ulong)(i + 1));
        });
        vv.Increment(actorId).Should().Be(1001);
    }

    [Fact]
    public void ConcurrentIncrementsAndMergesOnSeparateActorsWork()
    {
        var vv = new VersionContext<Guid>();
        var actorIncrement = Guid.NewGuid();
        var actorMerges = Guid.NewGuid();

        Parallel.For(0, 1000, i =>
        {
            if(i % 2 == 0) vv.Merge(actorMerges, (ulong)(i + 1));
            else vv.Increment(actorIncrement);
        });
        vv.Increment(actorIncrement).Should().Be(501, "There was 500 calls to increment before");
        vv.Increment(actorMerges).Should().Be(1000, "Last merge was with 999 (998 % 2 == 0 => merge (998 + 1))");
    }
}