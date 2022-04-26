using System;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Model.Ids.SourceGenerators;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Represents an InstanceId structure, which encapsulates string and allows to explicitly separate ids for different entities
    /// </summary>
    [GenerateId("instance", BackingFieldType = BackingFieldType.String)]
    [ProtoContract]
    public readonly partial struct InstanceId : IHashable
    {
        [ProtoMember(1)]
        private readonly string _id;

        public ReadOnlySpan<byte> CalculateHash()
        {
            return ToByteArray();
        }

        public static InstanceId FromShardId(ShardId id) => new(id.ToString());

        private static partial void AssertValid(string id) { }

        public static partial InstanceId GenerateNew() => new(Guid.NewGuid().ToString());
    }
}