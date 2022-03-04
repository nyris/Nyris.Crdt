using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    [ProtoContract(SkipConstructor = true)]
    public record AddValueOperation<TKey, TValue, TTimeStamp>([property: ProtoMember(1)] TKey Key,
        [property: ProtoMember(2)] TValue Value,
        [property: ProtoMember(3)] TTimeStamp TimeStamp) : RegistryOperation
        where TKey : IHashable
    {
        /// <inheritdoc />
        public override IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds)
        {
            var shards = shardIds.ToList();
            var hashStart = MemoryMarshal.Cast<byte, ushort>(Key.CalculateHash())[0];
            var step = ushort.MaxValue / shards.Count;
            return new []{ shards[hashStart / step] };
        }
    }
}