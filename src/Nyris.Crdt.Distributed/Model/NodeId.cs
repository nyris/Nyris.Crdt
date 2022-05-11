using Nyris.Crdt.Model;
using Nyris.Model.Ids.SourceGenerators;
using ProtoBuf;
using System;

namespace Nyris.Crdt.Distributed.Model;

/// <summary>
/// Represents an NodeId structure, which encapsulates string and allows to explicitly separate ids for different entities
/// </summary>
[GenerateId("node", BackingFieldType = BackingFieldType.String)]
[ProtoContract]
public readonly partial struct NodeId : IHashable
{
    [ProtoMember(1)]
    private readonly string _id;

    public ReadOnlySpan<byte> CalculateHash() => ToByteArray();

    private static partial void AssertValid(string id) { }

    public static partial NodeId GenerateNew() => new(Guid.NewGuid().ToString());
}
