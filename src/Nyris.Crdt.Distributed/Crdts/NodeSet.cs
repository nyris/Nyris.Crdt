using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>
    {
        private readonly NodeInfo _thisNodeInfo;

        /// <inheritdoc />
        public NodeSet(string id, INodeInfoProvider? nodeInfoProvider = null) : base(id)
        {
            _thisNodeInfo = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(OrSetDto other, CancellationToken cancellationToken = default)
        {
            var result = await base.MergeAsync(other, cancellationToken);

            if (!Value.Contains(_thisNodeInfo))
            {
                await AddAsync(_thisNodeInfo, _thisNodeInfo.Id);
            }

            return result;
        }

        public static readonly IManagedCRDTFactory<NodeSet, OrSetDto> DefaultFactory = new Factory();

        private sealed class Factory : IManagedCRDTFactory<NodeSet, OrSetDto>
        {
            /// <inheritdoc />
            public NodeSet Create(string instanceId) => new(instanceId);
        }
    }
}