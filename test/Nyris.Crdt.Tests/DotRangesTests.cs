using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nyris.Crdt.Model;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class DotRangesTests
{
    public static IEnumerable<object[]> MergeRangesData()
    {
        yield return new object[]
        {
            new Range[0],
            new Range(1, 2),
            new[] { new Range(1, 2) }
        };
        
        var singleRange = new[] { new Range(2, 5) };

        yield return new object[]
        {
            singleRange,
            new Range(1, 2),
            new[] { new Range(1, 5) }
        };
        yield return new object[]
        {
            singleRange,
            new Range(5, 6),
            new[] { new Range(2, 6) }
        };
        yield return new object[]
        {
            singleRange,
            new Range(1, 3),
            new[] { new Range(1, 5) }
        };
        yield return new object[]
        {
            singleRange,
            new Range(1, 5),
            new[] { new Range(1, 5) }
        };
        yield return new object[]
        {
            singleRange,
            new Range(2, 3),
            singleRange
        };
        yield return new object[]
        {
            singleRange,
            new Range(2, 5),
            singleRange
        };
        var disjointed = new Range(6, 7);
        yield return new object[]
        {
            singleRange,
            disjointed,
            singleRange.Append(disjointed)
        };

        // ################################  two ranges ################################
        var twoRanges = new[] { new Range(3, 5), new Range(7, 10) };
        
        // starts before all
        yield return new object[]
        {
            twoRanges,
            new Range(1, 2),
            twoRanges.Prepend(new Range(1, 2))
        };
        yield return new object[]
        {
            twoRanges,
            new Range(1, 3),
            new[] { new Range(1, 5), new Range(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(1, 4),
            new[] { new Range(1, 5), new Range(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(1, 5),
            new[] { new Range(1, 5), new Range(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(1, 6),
            new[] { new Range(1, 6), new Range(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(1, 7),
            new[] { new Range(1, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(1, 12),
            new[] { new Range(1, 12) }
        };
        
        // starts in the middle of first range
        yield return new object[]
        {
            twoRanges,
            new Range(4, 5),
            twoRanges
        };
        yield return new object[]
        {
            twoRanges,
            new Range(4, 6),
            new[] { new Range(3, 6), new Range(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(4, 8),
            new[] { new Range(3, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(4, 11),
            new[] { new Range(3, 11) }
        };
        
        // starts at the end of first range
        yield return new object[]
        {
            twoRanges,
            new Range(5, 6),
            new[] { new Range(3, 6), new Range(7, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(5, 7),
            new[] { new Range(3, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(5, 8),
            new[] { new Range(3, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(5, 12),
            new[] { new Range(3, 12) }
        };
        
        // starts in between of two ranges
        yield return new object[]
        {
            twoRanges,
            new Range(6, 7),
            new[] { new Range(3, 5), new Range(6, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(6, 9),
            new[] { new Range(3, 5), new Range(6, 10) }
        };
        yield return new object[]
        {
            twoRanges,
            new Range(6, 11),
            new[] { new Range(3, 5), new Range(6, 11) }
        };
        
        // starts at the start of second range
        yield return new object[]
        {
            twoRanges,
            new Range(7, 9),
            twoRanges
        };
        yield return new object[]
        {
            twoRanges,
            new Range(7, 11),
            new[] { new Range(3, 5), new Range(7, 11) }
        };
        
        // starts at the end of second range
        yield return new object[]
        {
            twoRanges,
            new Range(10, 12),
            new[] { new Range(3, 5), new Range(7, 12) }
        };
        
        // start after second range
        yield return new object[]
        {
            twoRanges,
            new Range(11, 12),
            twoRanges.Append(new Range(11, 12))
        };
        
        // ###############################################################################
        // ################################  three ranges ################################
        var threeRanges = new[] { new Range(3, 5), new Range(6, 8), new Range(13, 20) };
        
        // starts before all
        yield return new object[]
        {
            threeRanges,
            new Range(1, 2),
            threeRanges.Prepend(new Range(1, 2))
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 3),
            new[] { new Range(1, 5), new Range(6, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 4),
            new[] { new Range(1, 5), new Range(6, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 5),
            new[] { new Range(1, 5), new Range(6, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 6),
            new[] { new Range(1, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 7),
            new[] { new Range(1, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 12),
            new[] { new Range(1, 12), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 13),
            new[] { new Range(1, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(1, 21),
            new[] { new Range(1, 21) }
        };
        
        // starts in the middle of first range
        yield return new object[]
        {
            threeRanges,
            new Range(4, 5),
            threeRanges
        };
        yield return new object[]
        {
            threeRanges,
            new Range(4, 6),
            new[] { new Range(3, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(4, 8),
            new[] { new Range(3, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(4, 13),
            new[] { new Range(3, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(4, 23),
            new[] { new Range(3, 23) }
        };
        
        // starts at the end of first range
        yield return new object[]
        {
            threeRanges,
            new Range(5, 6),
            new[] { new Range(3, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(5, 7),
            new[] { new Range(3, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(5, 8),
            new[] { new Range(3, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(5, 12),
            new[] { new Range(3, 12), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(5, 13),
            new[] { new Range(3, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(5, 15),
            new[] { new Range(3, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(5, 22),
            new[] { new Range(3, 22) }
        };
        
        // starts at the start of second range
        yield return new object[]
        {
            threeRanges,
            new Range(6, 7),
            threeRanges
        };
        yield return new object[]
        {
            threeRanges,
            new Range(6, 9),
            new[] { new Range(3, 5), new Range(6, 9), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(6, 13),
            new[] { new Range(3, 5), new Range(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(6, 15),
            new[] { new Range(3, 5), new Range(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(6, 25),
            new[] { new Range(3, 5), new Range(6, 25) }
        };
        
        // starts in the middle of second range
        yield return new object[]
        {
            threeRanges,
            new Range(7, 8),
            new[] { new Range(3, 5), new Range(6, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(7, 9),
            new[] { new Range(3, 5), new Range(6, 9), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(7, 12),
            new[] { new Range(3, 5), new Range(6, 12), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(7, 13),
            new[] { new Range(3, 5), new Range(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(7, 16),
            new[] { new Range(3, 5), new Range(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(7, 24),
            new[] { new Range(3, 5), new Range(6, 24) }
        };
        
        // starts at the end of second range
        yield return new object[]
        {
            threeRanges,
            new Range(8, 9),
            new[] { new Range(3, 5), new Range(6, 9), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(8, 12),
            new[] { new Range(3, 5), new Range(6, 12), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(8, 13),
            new[] { new Range(3, 5), new Range(6, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(8, 21),
            new[] { new Range(3, 5), new Range(6, 21) }
        };
        
        // start between second and third range
        yield return new object[]
        {
            threeRanges,
            new Range(9, 10),
            new[] { new Range(3, 5), new Range(6, 8), new Range(9, 10), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(9, 13),
            new[] { new Range(3, 5), new Range(6, 8), new Range(9, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(9, 16),
            new[] { new Range(3, 5), new Range(6, 8), new Range(9, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(9, 22),
            new[] { new Range(3, 5), new Range(6, 8), new Range(9, 22) }
        };
        
        // start at the start of third range
        yield return new object[]
        {
            threeRanges,
            new Range(13, 14),
            new[] { new Range(3, 5), new Range(6, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(13, 21),
            new[] { new Range(3, 5), new Range(6, 8), new Range(13, 21) }
        };
        
        
        // start in the middle of third range
        yield return new object[]
        {
            threeRanges,
            new Range(15, 16),
            new[] { new Range(3, 5), new Range(6, 8), new Range(13, 20) }
        };
        yield return new object[]
        {
            threeRanges,
            new Range(15, 21),
            new[] { new Range(3, 5), new Range(6, 8), new Range(13, 21) }
        };
        
        // start at the end of third range
        yield return new object[]
        {
            threeRanges,
            new Range(20, 24),
            new[] { new Range(3, 5), new Range(6, 8), new Range(13, 24) }
        };
        
        // start after third range
        yield return new object[]
        {
            threeRanges,
            new Range(21, 22),
            threeRanges.Append(new Range(21, 22))
        };
        
        // ###############################################################################
        // ##################################  5 ranges ##################################
        // ###############################################################################
        
        var fiveRanges = new[]
        {
            new Range(3, 5), new Range(6, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
        };
        
        // starts before all
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 2),
            fiveRanges.Prepend(new Range(1, 2))
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 3),
            new[]
            {
                new Range(1, 5), new Range(6, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 4),
            new[]
            {
                new Range(1, 5), new Range(6, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 6),
            new[]
            {
                new Range(1, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 7),
            new[]
            {
                new Range(1, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 10),
            new[]
            {
                new Range(1, 10), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 13),
            new[]
            {
                new Range(1, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 15),
            new[]
            {
                new Range(1, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 16),
            new[]
            {
                new Range(1, 16), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 17),
            new[]
            {
                new Range(1, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 19),
            new[]
            {
                new Range(1, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 21),
            new[]
            {
                new Range(1, 21), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 22),
            new[]
            {
                new Range(1, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 24),
            new[]
            {
                new Range(1, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(1, 30),
            new[]
            {
                new Range(1, 30)
            }
        };
        
        // starts at the end of 1st
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 6),
            new[]
            {
                new Range(3, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 8),
            new[]
            {
                new Range(3, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 11),
            new[]
            {
                new Range(3, 11), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 13),
            new[]
            {
                new Range(3, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 14),
            new[]
            {
                new Range(3, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 16),
            new[]
            {
                new Range(3, 16), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 17),
            new[]
            {
                new Range(3, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 19),
            new[]
            {
                new Range(3, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 21),
            new[]
            {
                new Range(3, 21), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 22),
            new[]
            {
                new Range(3, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(5, 27),
            new[]
            {
                new Range(3, 27)
            }
        };
        
        // starts in 2-nd
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 8),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 10),
            new[]
            {
                new Range(3, 5), new Range(6, 10), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 13),
            new[]
            {
                new Range(3, 5), new Range(6, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 15),
            new[]
            {
                new Range(3, 5), new Range(6, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 17),
            new[]
            {
                new Range(3, 5), new Range(6, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 19),
            new[]
            {
                new Range(3, 5), new Range(6, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 21),
            new[]
            {
                new Range(3, 5), new Range(6, 21), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 22),
            new[]
            {
                new Range(3, 5), new Range(6, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(7, 29),
            new[]
            {
                new Range(3, 5), new Range(6, 29)
            }
        };
        
        // starts after 2nd
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 12),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 12), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 13),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 15),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 16),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 16), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 17),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 21),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 21), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 22),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(9, 28),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(9, 28)
            }
        };
        
        // starts after in the middle of 3-rd
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 15),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 15), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 16),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 16), new Range(17, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 17),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 19),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 20), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 21),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 21), new Range(22, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 22),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 25)
            }
        };
        yield return new object[]
        {
            fiveRanges,
            new Range(14, 26),
            new[]
            {
                new Range(3, 5), new Range(6, 8), new Range(13, 26)
            }
        };
        
        // ##### caught during debugging #####
        yield return new object[]
        {
            new Range[]
            {
                new(1, 31), new(32, 36), new(37, 38), new(39, 41), new(43, 54), new(55, 70), new(71, 76),
                new(77, 80), new(82, 96), new(97, 105), new(106, 107), new(109, 114), new(115, 118), new(119, 127)
            },
            new Range(31, 32),
            new Range[]
            {
                new(1, 36), new(37, 38), new(39, 41), new(43, 54), new(55, 70), new(71, 76),
                new(77, 80), new(82, 96), new(97, 105), new(106, 107), new(109, 114), new(115, 118), new(119, 127)
            }
        };
    }

    [Theory]
    [MemberData(nameof(MergeRangesData))]
    public void MergeRangesWork(IEnumerable<Range> startingData, Range range,
        IEnumerable<Range> expectedResult)
    {
        var ranges = new ConcurrentVersionRanges(startingData);
        ranges.Merge(range);
        ranges.ToArray().Should().BeEquivalentTo(expectedResult);
    }
    
    [Fact]
    public void DotRanges_GetNewWorks()
    {
        var ranges = new ConcurrentVersionRanges();

        ranges.GetNew().Should().Be(1);
        ranges.GetNew().Should().Be(2);
        ranges.GetNew().Should().Be(3);
    }
    
    [Fact]
    public void DotRanges_GetNewInParallelWorks()
    {
        var ranges = new ConcurrentVersionRanges();
        Parallel.For(0, 1000, _ =>
        {
            ranges.GetNew();
        });
        
        ranges.GetNew().Should().Be(1001);
    }
    
    [Fact]
    public void DotRanges_MergeWorks()
    {
        var ranges = new ConcurrentVersionRanges();

        ranges.Merge(1);
        ranges.GetNew().Should().Be(2);
        
        ranges.Merge(5);
        ranges.Merge(4);
        ranges.GetNew().Should().Be(6);
    }
    
    [Fact]
    public void DotRanges_ParallelMergesWorks()
    {
        var ranges = new ConcurrentVersionRanges();
        Parallel.For(1, 1000, i =>
        {
            ranges.Merge((ulong)i);
        });
        
        ranges.GetNew().Should().Be(1000);
    }
    
    [Fact]
    public void DotRanges_ConnectingMergeWorks()
    {
        var ranges = new ConcurrentVersionRanges();

        ranges.Merge(2);
        ranges.ToArray().Should().Contain(new Range(2, 3)).And.HaveCount(1);
        
        ranges.Merge(5);
        ranges.Merge(4);
        ranges.ToArray().Should()
            .Contain(new Range(2, 3))
            .And.Contain(new Range(4, 6))
            .And.HaveCount(2);
        
        ranges.Merge(3);
        ranges.ToArray().Should().Contain(new Range(2, 6)).And.HaveCount(1);
        
        ranges.Merge(1);
        ranges.ToArray().Should().Contain(new Range(1, 6)).And.HaveCount(1);
    }
}