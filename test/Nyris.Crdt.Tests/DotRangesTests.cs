using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class DotRangesTests
{
    public static IEnumerable<object[]> MergeRangesData()
    {
        yield return new object[]
        {
            new DotRange[0],
            new DotRange(1, 2),
            new[] { new DotRange(1, 2) }
        };
        
        var singleRange = new[] { new DotRange(2, 5) };

        yield return new object[]
        {
            singleRange,
            new DotRange(1, 2),
            new[] { new DotRange(1, 5) }
        };
        yield return new object[]
        {
            singleRange,
            new DotRange(5, 6),
            new[] { new DotRange(2, 6) }
        };
        yield return new object[]
        {
            singleRange,
            new DotRange(1, 3),
            new[] { new DotRange(1, 5) }
        };
        yield return new object[]
        {
            singleRange,
            new DotRange(1, 5),
            new[] { new DotRange(1, 5) }
        };
        yield return new object[]
        {
            singleRange,
            new DotRange(2, 3),
            singleRange
        };
        yield return new object[]
        {
            singleRange,
            new DotRange(2, 5),
            singleRange
        };
        var disjointed = new DotRange(6, 7);
        yield return new object[]
        {
            singleRange,
            disjointed,
            singleRange.Append(disjointed)
        };

        // ################################  two ranges ################################
        var twoRanges = new[] { new DotRange(3, 5), new DotRange(7, 10) };
        
        // starts before all
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 2),
            twoRanges.Prepend(new DotRange(1, 2))
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 3),
            new[] { new DotRange(1, 5), new DotRange(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 4),
            new[] { new DotRange(1, 5), new DotRange(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 5),
            new[] { new DotRange(1, 5), new DotRange(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 6),
            new[] { new DotRange(1, 6), new DotRange(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 7),
            new[] { new DotRange(1, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(1, 12),
            new[] { new DotRange(1, 12) }
        };
        
        // starts in the middle of first range
        yield return new object[]
        {
            twoRanges,
            new DotRange(4, 5),
            twoRanges
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(4, 6),
            new[] { new DotRange(3, 6), new DotRange(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(4, 8),
            new[] { new DotRange(3, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(4, 11),
            new[] { new DotRange(3, 11) }
        };
        
        // starts at the end of first range
        yield return new object[]
        {
            twoRanges,
            new DotRange(5, 6),
            new[] { new DotRange(3, 6), new DotRange(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(5, 7),
            new[] { new DotRange(3, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(5, 8),
            new[] { new DotRange(3, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(5, 12),
            new[] { new DotRange(3, 12) }
        };
        
        // starts in between of two ranges
        yield return new object[]
        {
            twoRanges,
            new DotRange(6, 7),
            new[] { new DotRange(3, 5), new DotRange(6, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(6, 9),
            new[] { new DotRange(3, 5), new DotRange(6, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(6, 11),
            new[] { new DotRange(3, 5), new DotRange(6, 11) }
        };
        
        // starts at the start of second range
        yield return new object[]
        {
            twoRanges,
            new DotRange(7, 9),
            twoRanges
        };
        yield return new object[]
        {
            twoRanges,
            new DotRange(7, 11),
            new[] { new DotRange(3, 5), new DotRange(7, 11) }
        };
        
        // starts at the end of second range
        yield return new object[]
        {
            twoRanges,
            new DotRange(10, 12),
            new[] { new DotRange(3, 5), new DotRange(7, 12) }
        };
        
        // start after second range
        yield return new object[]
        {
            twoRanges,
            new DotRange(11, 12),
            twoRanges.Append(new DotRange(11, 12))
        };
        
        // ###############################################################################
        // ################################  three ranges ################################
        var threeRanges = new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 20) };
        
        // starts before all
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 2),
            threeRanges.Prepend(new DotRange(1, 2))
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 3),
            new[] { new DotRange(1, 5), new DotRange(6, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 4),
            new[] { new DotRange(1, 5), new DotRange(6, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 5),
            new[] { new DotRange(1, 5), new DotRange(6, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 6),
            new[] { new DotRange(1, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 7),
            new[] { new DotRange(1, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 12),
            new[] { new DotRange(1, 12), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 13),
            new[] { new DotRange(1, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(1, 21),
            new[] { new DotRange(1, 21) }
        };
        
        // starts in the middle of first range
        yield return new object[]
        {
            threeRanges,
            new DotRange(4, 5),
            threeRanges
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(4, 6),
            new[] { new DotRange(3, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(4, 8),
            new[] { new DotRange(3, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(4, 13),
            new[] { new DotRange(3, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(4, 23),
            new[] { new DotRange(3, 23) }
        };
        
        // starts at the end of first range
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 6),
            new[] { new DotRange(3, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 7),
            new[] { new DotRange(3, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 8),
            new[] { new DotRange(3, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 12),
            new[] { new DotRange(3, 12), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 13),
            new[] { new DotRange(3, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 15),
            new[] { new DotRange(3, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(5, 22),
            new[] { new DotRange(3, 22) }
        };
        
        // starts at the start of second range
        yield return new object[]
        {
            threeRanges,
            new DotRange(6, 7),
            threeRanges
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(6, 9),
            new[] { new DotRange(3, 5), new DotRange(6, 9), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(6, 13),
            new[] { new DotRange(3, 5), new DotRange(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(6, 15),
            new[] { new DotRange(3, 5), new DotRange(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(6, 25),
            new[] { new DotRange(3, 5), new DotRange(6, 25) }
        };
        
        // starts in the middle of second range
        yield return new object[]
        {
            threeRanges,
            new DotRange(7, 8),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(7, 9),
            new[] { new DotRange(3, 5), new DotRange(6, 9), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(7, 12),
            new[] { new DotRange(3, 5), new DotRange(6, 12), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(7, 13),
            new[] { new DotRange(3, 5), new DotRange(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(7, 16),
            new[] { new DotRange(3, 5), new DotRange(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(7, 24),
            new[] { new DotRange(3, 5), new DotRange(6, 24) }
        };
        
        // starts at the end of second range
        yield return new object[]
        {
            threeRanges,
            new DotRange(8, 9),
            new[] { new DotRange(3, 5), new DotRange(6, 9), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(8, 12),
            new[] { new DotRange(3, 5), new DotRange(6, 12), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(8, 13),
            new[] { new DotRange(3, 5), new DotRange(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(8, 21),
            new[] { new DotRange(3, 5), new DotRange(6, 21) }
        };
        
        // start between second and third range
        yield return new object[]
        {
            threeRanges,
            new DotRange(9, 10),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 10), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(9, 13),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(9, 16),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(9, 22),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 22) }
        };
        
        // start at the start of third range
        yield return new object[]
        {
            threeRanges,
            new DotRange(13, 14),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(13, 21),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 21) }
        };
        
        
        // start in the middle of third range
        yield return new object[]
        {
            threeRanges,
            new DotRange(15, 16),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new DotRange(15, 21),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 21) }
        };
        
        // start at the end of third range
        yield return new object[]
        {
            threeRanges,
            new DotRange(20, 24),
            new[] { new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 24) }
        };
        
        // start after third range
        yield return new object[]
        {
            threeRanges,
            new DotRange(21, 22),
            threeRanges.Append(new DotRange(21, 22))
        };
        
        // ###############################################################################
        // ##################################  5 ranges ##################################
        // ###############################################################################
        
        var fiveRanges = new[]
        {
            new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
        };
        
        // starts before all
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 2),
            fiveRanges.Prepend(new DotRange(1, 2))
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 3),
            new[]
            {
                new DotRange(1, 5), new DotRange(6, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 4),
            new[]
            {
                new DotRange(1, 5), new DotRange(6, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 6),
            new[]
            {
                new DotRange(1, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 7),
            new[]
            {
                new DotRange(1, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 10),
            new[]
            {
                new DotRange(1, 10), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 13),
            new[]
            {
                new DotRange(1, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 15),
            new[]
            {
                new DotRange(1, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 16),
            new[]
            {
                new DotRange(1, 16), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 17),
            new[]
            {
                new DotRange(1, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 19),
            new[]
            {
                new DotRange(1, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 21),
            new[]
            {
                new DotRange(1, 21), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 22),
            new[]
            {
                new DotRange(1, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 24),
            new[]
            {
                new DotRange(1, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(1, 30),
            new[]
            {
                new DotRange(1, 30)
            }
        };
        
        // starts at the end of 1st
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 6),
            new[]
            {
                new DotRange(3, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 8),
            new[]
            {
                new DotRange(3, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 11),
            new[]
            {
                new DotRange(3, 11), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 13),
            new[]
            {
                new DotRange(3, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 14),
            new[]
            {
                new DotRange(3, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 16),
            new[]
            {
                new DotRange(3, 16), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 17),
            new[]
            {
                new DotRange(3, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 19),
            new[]
            {
                new DotRange(3, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 21),
            new[]
            {
                new DotRange(3, 21), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 22),
            new[]
            {
                new DotRange(3, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(5, 27),
            new[]
            {
                new DotRange(3, 27)
            }
        };
        
        // starts in 2-nd
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 8),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 10),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 10), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 13),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 15),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 17),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 19),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 21),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 21), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 22),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(7, 29),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 29)
            }
        };
        
        // starts after 2nd
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 12),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 12), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 13),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 15),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 16),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 16), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 17),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 21),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 21), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 22),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(9, 28),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(9, 28)
            }
        };
        
        // starts after in the middle of 3-rd
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 15),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 15), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 16),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 16), new DotRange(17, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 17),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 19),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 20), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 21),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 21), new DotRange(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 22),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new DotRange(14, 26),
            new[]
            {
                new DotRange(3, 5), new DotRange(6, 8), new DotRange(13, 26)
            }
        };
    }

    [Theory]
    [MemberData(nameof(MergeRangesData))]
    public void MergeRangesWork(IEnumerable<DotRange> startingData, DotRange range,
        IEnumerable<DotRange> expectedResult)
    {
        var ranges = new DotRanges(startingData);
        ranges.Merge(range);
        ranges.ToArray().Should().BeEquivalentTo(expectedResult);
    }
    
    [Fact]
    public void DotRanges_GetNewWorks()
    {
        var ranges = new DotRanges();

        ranges.GetNew().Should().Be(1);
        ranges.GetNew().Should().Be(2);
        ranges.GetNew().Should().Be(3);
    }
    
    [Fact]
    public void DotRanges_GetNewInParallelWorks()
    {
        var ranges = new DotRanges();
        Parallel.For(0, 1000, _ =>
        {
            ranges.GetNew();
        });
        
        ranges.GetNew().Should().Be(1001);
    }
    
    [Fact]
    public void DotRanges_MergeWorks()
    {
        var ranges = new DotRanges();

        ranges.Merge(1);
        ranges.GetNew().Should().Be(2);
        
        ranges.Merge(5);
        ranges.Merge(4);
        ranges.GetNew().Should().Be(6);
    }
    
    [Fact]
    public void DotRanges_ParallelMergesWorks()
    {
        var ranges = new DotRanges();
        Parallel.For(1, 1000, i =>
        {
            ranges.Merge((ulong)i);
        });
        
        ranges.GetNew().Should().Be(1000);
    }
    
    [Fact]
    public void DotRanges_ConnectingMergeWorks()
    {
        var ranges = new DotRanges();

        ranges.Merge(2);
        ranges.ToArray().Should().Contain(new DotRange(2, 3)).And.HaveCount(1);
        
        ranges.Merge(5);
        ranges.Merge(4);
        ranges.ToArray().Should()
            .Contain(new DotRange(2, 3))
            .And.Contain(new DotRange(4, 6))
            .And.HaveCount(2);
        
        ranges.Merge(3);
        ranges.ToArray().Should().Contain(new DotRange(2, 6)).And.HaveCount(1);
        
        ranges.Merge(1);
        ranges.ToArray().Should().Contain(new DotRange(1, 6)).And.HaveCount(1);
    }
}