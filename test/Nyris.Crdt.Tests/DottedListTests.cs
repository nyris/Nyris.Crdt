using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class DottedListTests
{
    public static IEnumerable<object[]> RangesData()
    {
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(1, 2)},
            new[] {new DotRange(1, 2)}
        };
        
        // ######################################## single dot ########################################
        // known range starts before dot
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(1, 2)},
            new[] {new DotRange(1, 2)}
        };
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(1, 3)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(1, 4)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(1, 5)},
            new[] {new DotRange(1, 3), new DotRange(4, 5)}
        };

        // known range starts at the dot
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(3, 4)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(3, 5)},
            new[] {new DotRange(4, 5)}
        };
        
        // known range after dot
        yield return new object[] 
        {
            new ulong[] {3},
            new[] {new DotRange(4, 7)},
            new[] {new DotRange(4, 7)}
        };
        
        // ######################################## consecutive dots ########################################
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 2)},
            new[] {new DotRange(1, 2)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 3)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 4)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 5)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 6)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 7)},
            new[] {new DotRange(1, 3), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(1, 17)},
            new[] {new DotRange(1, 3), new DotRange(6, 17)}
        };
        
        // start in the middle of dots
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(3, 5)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(3, 6)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(3, 7)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(3, 9)},
            new[] {new DotRange(6, 9)}
        };
        
        // start after
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(6, 7)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] {3, 4, 5},
            new[] {new DotRange(6, 9)},
            new[] {new DotRange(6, 9)}
        };

        // ############################### consecutive dots with gaps ##################################
        // start before all dots
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 2)},
            new[] {new DotRange(1, 2)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 3)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 4)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 5)},
            new[] {new DotRange(1, 3), new DotRange(4, 5)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 6)},
            new[] {new DotRange(1, 3), new DotRange(4, 5)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 7)},
            new[] {new DotRange(1, 3), new DotRange(4, 5), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 8)},
            new[] {new DotRange(1, 3), new DotRange(4, 5), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 9)},
            new[] {new DotRange(1, 3), new DotRange(4, 5), new DotRange(6, 7), new DotRange(8, 9)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(1, 19)},
            new[] {new DotRange(1, 3), new DotRange(4, 5), new DotRange(6, 7), new DotRange(8, 19)}
        };
        
        // start at the first dot
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(3, 4)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(3, 5)},
            new[] {new DotRange(4, 5)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(3, 6)},
            new[] {new DotRange(4, 5)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(3, 7)},
            new[] {new DotRange(4, 5), new DotRange(6, 7)}
        };
        
        // start after first dot
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(4, 5)},
            new[] {new DotRange(4, 5)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(4, 6)},
            new[] {new DotRange(4, 5)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(4, 7)},
            new[] {new DotRange(4, 5), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(4, 8)},
            new[] {new DotRange(4, 5), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(4, 9)},
            new[] {new DotRange(4, 5), new DotRange(6, 7), new DotRange(8, 9)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 5, 7 },
            new[] {new DotRange(4, 19)},
            new[] {new DotRange(4, 5), new DotRange(6, 7), new DotRange(8, 19)}
        };
        
        // ############################### dot ranges with gaps ##################################
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 2)},
            new[] {new DotRange(1, 2)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 4)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 6)},
            new[] {new DotRange(1, 3)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 7)},
            new[] {new DotRange(1, 3), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 8)},
            new[] {new DotRange(1, 3), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 10)},
            new[] {new DotRange(1, 3), new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 11)},
            new[] {new DotRange(1, 3), new DotRange(6, 7), new DotRange(10, 11)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(1, 21)},
            new[] {new DotRange(1, 3), new DotRange(6, 7), new DotRange(10, 21)}
        };
        
        // start inside first range
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(4, 5)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(4, 6)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(4, 7)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(4, 8)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(4, 10)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(4, 12)},
            new[] {new DotRange(6, 7), new DotRange(10, 12)}
        };
        
        // start in the middle
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(6, 7)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(6, 8)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(6, 10)},
            new[] {new DotRange(6, 7)}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(6, 12)},
            new[] {new DotRange(6, 7), new DotRange(10, 12)}
        };
        
        // start in the second range
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(7, 8)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(7, 10)},
            new DotRange[] {}
        };
        yield return new object[] 
        {
            new ulong[] { 3, 4, 5, 7, 8, 9 },
            new[] {new DotRange(7, 12)},
            new[] {new DotRange(10, 12)}
        };
        
        // manually found edge cases:
        // yield return new object[]
        // {
        //     new ulong[] { 546, 547, 548, 549, 550, 551, 552 },
        //     new[] { new DotRange(1, 545), new DotRange(546, 553) },
        //     new[] { new DotRange(1, 545) }
        // };
        yield return new object[]
        {
            new ulong[] { 10, 11, 12 },
            new[] { new DotRange(1, 9), new DotRange(10, 13) },
            new[] { new DotRange(1, 9) }
        };
    }

    [Theory]
    [MemberData(nameof(RangesData))]
    public void GetEmptyRangesWorks(IEnumerable<ulong> dots,
        IEnumerable<DotRange> known,
        IEnumerable<DotRange> expected)
    {
        var list = new DottedList<double>();
        foreach (var dot in dots)
        {
            list.TryAdd(Random.Shared.NextDouble(), dot);
        }
        
        var ranges = list.GetEmptyRanges(known.ToList());
        ranges.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(20)]
    public void ConcurrentAddsWork(int n)
    {
        CheckConcurrentAdds(DateTime.Now, n);
        CheckConcurrentAdds(1, n);
        CheckConcurrentAdds(3.14, n);
        CheckConcurrentAdds(Guid.NewGuid(), n);
        CheckConcurrentAdds(new LargeStruct(), n);
        CheckConcurrentAdds(new Record(1, 1.1f), n);
    }

    [Fact]
    public void GetRanges_EmptyList_Works()
    {
        var list = new DottedList<double>();
        
        var ranges = list.GetEmptyRanges(Array.Empty<DotRange>()).ToList();
        ranges.Should().HaveCount(0);
        
        var expectedRange = new DotRange(1, 5);
        ranges = list.GetEmptyRanges(new[] { expectedRange }).ToList();
        ranges.Should()
            .Contain(expectedRange)
            .And.HaveCount(1);
        
        expectedRange = new DotRange(3, 5);
        ranges = list.GetEmptyRanges(new[] { expectedRange }).ToList();
        ranges.Should()
            .Contain(expectedRange)
            .And.HaveCount(1);
        
        expectedRange = new DotRange(1, 2);
        ranges = list.GetEmptyRanges(new[] { expectedRange }).ToList();
        ranges.Should()
            .Contain(expectedRange)
            .And.HaveCount(1);
    }
    
    [Fact]
    public void GetRanges_SingleKnown_Works()
    {
        var list = new DottedList<double>();
        list.TryAdd(Random.Shared.NextDouble(), 3);
        list.TryAdd(Random.Shared.NextDouble(), 8);
        list.TryAdd(Random.Shared.NextDouble(), 9);
        list.TryAdd(Random.Shared.NextDouble(), 11);
        list.TryAdd(Random.Shared.NextDouble(), 12);
        list.TryAdd(Random.Shared.NextDouble(), 13);
        list.TryAdd(Random.Shared.NextDouble(), 14);

        var ranges = list.GetEmptyRanges(Array.Empty<DotRange>()).ToList();
        ranges.Should().HaveCount(0);

        ranges = list.GetEmptyRanges(Ranges(1, 2)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 2))
            .And.HaveCount(1);
        
        ranges = list.GetEmptyRanges(Ranges(1, 4)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.HaveCount(1);
        
        ranges = list.GetEmptyRanges(Ranges(1, 8)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.HaveCount(2);

        ranges = list.GetEmptyRanges(Ranges(1, 13)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.Contain(new DotRange(10, 11))
            .And.HaveCount(3);
        
        ranges = list.GetEmptyRanges(Ranges(1, 16)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.Contain(new DotRange(10, 11))
            .And.Contain(new DotRange(15, 16))
            .And.HaveCount(4);
        
        ranges = list.GetEmptyRanges(Ranges(1, 100)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.Contain(new DotRange(10, 11))
            .And.Contain(new DotRange(15, 100))
            .And.HaveCount(4);
    }
    
    [Fact]
    public void GetRanges_SeveralKnown_Works()
    {
        var list = new DottedList<double>();
        list.TryAdd(Random.Shared.NextDouble(), 3);
        list.TryAdd(Random.Shared.NextDouble(), 8);
        list.TryAdd(Random.Shared.NextDouble(), 9);
        list.TryAdd(Random.Shared.NextDouble(), 11);
        list.TryAdd(Random.Shared.NextDouble(), 12);
        list.TryAdd(Random.Shared.NextDouble(), 13);
        list.TryAdd(Random.Shared.NextDouble(), 14);
        
        var ranges = list.GetEmptyRanges(Ranges(1, 2, 3, 4)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 2))
            .And.HaveCount(1);
        
        ranges = list.GetEmptyRanges(Ranges(1, 4, 5, 7)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(5, 7))
            .And.HaveCount(2);
        
        ranges = list.GetEmptyRanges(Ranges(1, 4, 5, 9)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(5, 8))
            .And.HaveCount(2);

        ranges = list.GetEmptyRanges(Ranges(1, 10, 11, 16)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.Contain(new DotRange(15, 16))
            .And.HaveCount(3);

        ranges = list.GetEmptyRanges(Ranges(1, 10, 11, 16, 20, 100)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.Contain(new DotRange(15, 16))
            .And.Contain(new DotRange(20, 100))
            .And.HaveCount(4);
        
        ranges = list.GetEmptyRanges(Ranges(1, 10, 11, 16, 20, 100, 101, 103)).ToList();
        ranges.Should()
            .Contain(new DotRange(1, 3))
            .And.Contain(new DotRange(4, 8))
            .And.Contain(new DotRange(15, 16))
            .And.Contain(new DotRange(20, 100))
            .And.Contain(new DotRange(101, 103))
            .And.HaveCount(5);
    }

    private DotRange[] Ranges(params ulong[] dots)
    {
        Debug.Assert(dots.Length % 2 == 0);
        var result = new DotRange[dots.Length / 2];
        for (var i = 0; i < result.Length; ++i)
        {
            result[i] = new DotRange(dots[2 * i], dots[2 * i + 1]);
        }

        return result;
    }
    
    private void CheckConcurrentAdds<T>(T item, int n) where T : IEquatable<T>
    {
        var list = new DottedList<T>();
        Parallel.For(0, n, i =>
        {
            list.TryAdd(item, (ulong)i);
        });
        list.TryGetValue((ulong)(n - 1), out var returnedItem).Should().BeTrue();
        returnedItem.Should().NotBe(null);
        returnedItem.Should().Be(item);
        for (var i = 0; i < n - 1; ++i)
        {
            list.TryGetValue((ulong)i, out _).Should().BeFalse();   
        }
    }
    
    public readonly struct LargeStruct :  IEquatable<LargeStruct>
    {
        public readonly double V0 = Random.Shared.Next();
        public readonly double V1 = Random.Shared.Next();
        public readonly double V2 = Random.Shared.Next();
        public readonly double V3 = Random.Shared.Next();
        public readonly double V4 = Random.Shared.Next();
        public readonly double V5 = Random.Shared.Next();
        public readonly double V6 = Random.Shared.Next();
        public readonly double V7 = Random.Shared.Next();
        public readonly double V8 = Random.Shared.Next();
        public readonly double V9 = Random.Shared.Next();

        public LargeStruct()
        {
        }

        public bool Equals(LargeStruct other) => V0.Equals(other.V0);
        public override bool Equals(object? obj) => obj is LargeStruct other && Equals(other);
        public override int GetHashCode() => V0.GetHashCode();
    }

    public sealed record Record(int Value1, float Value2);
}