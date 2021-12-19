using Nyris.Crdt.Distributed.Strategies.PartialReplication;

namespace Nyris.Crdt.Distributed
{
    public static class DefaultConfiguration
    {
        public static IPartialReplicationStrategy PartialReplicationStrategy = new SimplePartialReplicationStrategy();
    }
}