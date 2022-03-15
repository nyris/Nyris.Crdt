using System;
using System.IO;
using FluentAssertions;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void ShardIdSerializable() => TypeSerializable(ShardId.New());

    [Fact]
    public void InstanceIdSerializable() => TypeSerializable(InstanceId.New());

    [Fact]
    public void CollectionIdSerializable() => TypeSerializable(CollectionId.New());

    [Fact]
    public void TypeNameAndInstanceIdSerializable()
        => TypeSerializable(new TypeNameAndInstanceId("name", InstanceId.New()));

    [Fact]
    public void HashAndInstanceIdSerializable()
        => TypeSerializable(new HashAndInstanceId(HashingHelper.CalculateHash(Random.Shared.NextDouble()).ToArray(),
            InstanceId.New()));

    [Fact]
    public void TypeNameAndHashSerializable()
        => TypeSerializable(new TypeNameAndHash("name",
            HashingHelper.CalculateHash(Random.Shared.NextDouble()).ToArray()));

    [Fact]
    public void TypeNameAndHashSerializable_EmptyHash()
        => TypeSerializable(new TypeNameAndHash("name", Array.Empty<byte>()));

    [Fact]
    public void NodeInfoSerializable()
        => TypeSerializable(new NodeInfo(new Uri("about:blank"), NodeId.New()));



    private void TypeSerializable<T>(T original)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Seek(0, SeekOrigin.Begin);
        var result = Serializer.Deserialize<T>(stream);
        result.Should().BeEquivalentTo(original);
    }
}