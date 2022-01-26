using Nyris.Crdt.Distributed.Strategies.PartialReplication;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed
{
    public static class DefaultConfiguration
    {
        public static readonly IPartialReplicationStrategy PartialReplicationStrategy = new SimplePartialReplicationStrategy();
        public static readonly INodeInfoProvider NodeInfoProvider = new NodeInfoProvider();
    }
}