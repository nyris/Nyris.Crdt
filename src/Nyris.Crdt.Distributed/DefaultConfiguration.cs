using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed
{
    public static class DefaultConfiguration
    {
        public static IPartialReplicationStrategy PartialReplicationStrategy = new SimplePartialReplicationStrategy();
        public static INodeInfoProvider NodeInfoProvider = new NodeInfoProvider();
        public static IResponseCombinator ResponseCombinator = new DefaultResponseCombinator();
    }
}