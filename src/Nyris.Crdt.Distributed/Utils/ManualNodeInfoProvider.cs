using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils
{
    internal sealed class ManualNodeInfoProvider : INodeInfoProvider
    {
        private readonly NodeInfo _nodeInfo;
        public ManualNodeInfoProvider(NodeId id, NodeInfo nodeInfo)
        {
            ThisNodeId = id;
            _nodeInfo = nodeInfo;
        }

        /// <inheritdoc />
        public NodeId ThisNodeId { get; }

        /// <inheritdoc />
        public NodeInfo GetMyNodeInfo() => _nodeInfo;
    }
}