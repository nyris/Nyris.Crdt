using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Distributed.Crdts.Operations
{
    public abstract record OperationWithKey<TKey> : RegistryOperation where TKey : IHashable
    {
        public abstract TKey Key { get; init; }

        /// <inheritdoc />
        public override IEnumerable<ShardId> GetShards(IEnumerable<ShardId> shardIds)
        {
            var shards = shardIds.ToList();
            if (shards.Count == 0) return shards;

            var hash = MemoryMarshal.Cast<byte, ushort>(Key.CalculateHash());
            if (hash.Length == 0) return shards;

            var hashStart = hash[0];
            var step = ushort.MaxValue / shards.Count;
            return new[] { shards[(hashStart / step) % shards.Count] };
        }
    }
}
