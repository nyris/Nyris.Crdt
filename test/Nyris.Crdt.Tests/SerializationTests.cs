using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using Xunit;

namespace Nyris.Crdt.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void ShardIdSerializable() => TypeSerializable(ShardId.GenerateNew());

    [Fact]
    public void InstanceIdSerializable() => TypeSerializable(InstanceId.GenerateNew());

    [Fact]
    public void CollectionIdSerializable() => TypeSerializable(CollectionId.New());

    [Fact]
    public void TypeNameAndInstanceIdSerializable()
        => TypeSerializable(new TypeNameAndInstanceId("name", InstanceId.GenerateNew()));

    [Fact]
    public void HashAndInstanceIdSerializable()
        => TypeSerializable(new HashAndInstanceId(HashingHelper.CalculateHash(Random.Shared.NextDouble()).ToArray(),
            InstanceId.GenerateNew()));

    [Fact]
    public void TypeNameAndHashSerializable()
        => TypeSerializable(new TypeNameAndHash("name",
            HashingHelper.CalculateHash(Random.Shared.NextDouble()).ToArray()));

    [Fact]
    public void TypeNameAndHashSerializable_EmptyHash()
        => TypeSerializable(new TypeNameAndHash("name", Array.Empty<byte>()));

    [Fact]
    public void NodeInfoSerializable()
        => TypeSerializable(new NodeInfo(new Uri("about:blank"), new NodeId(Guid.NewGuid().ToString())));

    [Fact]
    public void NodeSetDtoSerializable()
    {
        var id = NodeId.GenerateNew();
        var nodeInfo = new NodeInfo(new Uri("about:blank"), id);

        TypeSerializable(new NodeSet.NodeSetDto
        {
            Items = new HashSet<DottedItem<NodeId, NodeInfo>>
            {
                new(new Dot<NodeId>(id, 1), nodeInfo)
            },
            Tombstones = new HashSet<Tombstone<NodeId>>
            {
                new(new Dot<NodeId>(id, 2))
            },
            VersionVectors = new Dictionary<NodeId, VersionVector<NodeId>>
            {
                { id, new VersionVector<NodeId>(id, 2) }
            }
        });
    }

    private void TypeSerializable<T>(T original)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Seek(0, SeekOrigin.Begin);
        var result = Serializer.Deserialize<T>(stream);
        result.Should().BeEquivalentTo(original);
    }
}
