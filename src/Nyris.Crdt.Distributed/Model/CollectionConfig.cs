using System.Collections.Generic;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;

namespace Nyris.Crdt.Distributed.Model
{
    public sealed class CollectionConfig
    {
        public string Name { get; init; } = "";
        public IPartialReplicationStrategy? PartialReplicationStrategy { get; init; }
        public IEnumerable<string>? IndexNames { get; init; }
        public ShardingConfig? ShardingConfig { get; init; }
    }
}