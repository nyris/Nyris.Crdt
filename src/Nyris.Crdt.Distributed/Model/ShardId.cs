using Nyris.Crdt.Model;
using Nyris.Model.Ids.SourceGenerators;
using ProtoBuf;
using System;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Encapsulates Guid
    /// </summary>
    [GenerateId("shard")]
    [ProtoContract]
    public readonly partial struct ShardId : IHashable
    {
        [ProtoMember(1)]
        private readonly Guid _id;

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();

        public static ShardId FromInstanceId(InstanceId id) => new(id.ToString());
    }
}
