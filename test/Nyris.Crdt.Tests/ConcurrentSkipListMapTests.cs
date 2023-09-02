using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class ConcurrentSkipListMapTests
{
    private readonly ConcurrentSkipListMap<long, double> _map = new();
    private readonly Random _random= new(1);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(10000)]
    public void InsertSequentialWorks(int count)
    {
        for (var i = 0; i < count; ++i)
        {
            _map.TryAdd(i, i).Should().BeTrue();
        }

        for (var i = 0; i < count; ++i)
        {
            _map.TryGetValue(i, out var v).Should().BeTrue();
            v.Should().Be(i);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    [InlineData(10000)]
    public void InsertRandomWorks(int count)
    {
        var dict = new Dictionary<long, double>();
        for (var i = 0; i < count; ++i)
        {
            var key = _random.NextInt64();
            var value = _random.NextDouble();

            var mapResult = _map.TryAdd(key, value);
            var dictResult = dict.TryAdd(key, value);
            mapResult.Should().Be(dictResult);
        }

        foreach (var (key, expectedValue) in dict)
        {
            _map.TryGetValue(key, out var value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(500)]
    [InlineData(10000)]
    public void InsertAndRemoveRandomWorks(int count)
    {
        var dict = new Dictionary<long, double>();
        for (var i = 0; i < count; ++i)
        {
            if (_random.Next() % 2 == 0)
            {
                var key = _random.NextInt64(-30, 30);
                var value = _random.NextDouble();

                var insertedMap = _map.TryAdd(key, value);
                var insertedDict = dict.TryAdd(key, value);
                insertedMap.Should().Be(insertedDict);
            }
            else
            {
                var key = _random.NextInt64(-30, 30);

                var mapResult = _map.TryRemove(key, out var value);
                var dictResult = dict.Remove(key, out var expectedValue);
                mapResult.Should().Be(dictResult);
                value.Should().Be(expectedValue);
            }
        }

        foreach (var (key, expectedValue) in dict)
        {
            _map.TryGetValue(key, out var value).Should().BeTrue();
            value.Should().Be(expectedValue);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(500)]
    public void EnumerationWorks(int count)
    {
        for (var i = count - 1; i >= 0; --i)
        {
            _map.TryAdd(i, i).Should().BeTrue();
        }

        var elements = _map.ToList();

        elements.Should().HaveCount(count);
        elements.Select(pair => pair.Key).Should().BeInAscendingOrder();
    }

    [Fact]
    public void EnumerationWorksWithLimits()
    {
        for (var i = 10; i >= 0; --i)
        {
            _map.TryAdd(i, i).Should().BeTrue();
        }

        var keys = _map.WithinRange(-1, 11).Select(pair => pair.Key).ToList();
        keys.Should().BeEquivalentTo(Enumerable.Range(0, 11));

        keys = _map.WithinRange(0, 11).Select(pair => pair.Key).ToList();
        keys.Should().BeEquivalentTo(Enumerable.Range(0, 11));

        keys = _map.WithinRange(1, 11).Select(pair => pair.Key).ToList();
        keys.Should().BeEquivalentTo(Enumerable.Range(1, 10));

        keys = _map.WithinRange(1, 3).Select(pair => pair.Key).ToList();
        keys.Should().BeEquivalentTo(Enumerable.Range(1, 2));

        keys = _map.WithinRange(-2, 1).Select(pair => pair.Key).ToList();
        keys.Should().BeEquivalentTo(Enumerable.Range(0, 1));

        keys = _map.WithinRange(10, 12).Select(pair => pair.Key).ToList();
        keys.Should().BeEquivalentTo(Enumerable.Range(10, 1));
    }
}