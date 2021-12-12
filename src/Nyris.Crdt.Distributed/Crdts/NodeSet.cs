using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>
    {
        private static readonly NodeInfo ThisNodeInfo = NodeInfoProvider.GetMyNodeInfo();

        /// <inheritdoc />
        public NodeSet(string id) : base(id)
        {
        }

        private NodeSet(OrSetDto orSetDto, string instanceId) : base(orSetDto, instanceId)
        {
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo> other,
            CancellationToken cancellationToken = default)
        {
            var result = await base.MergeAsync(other, cancellationToken);

            if (!Value.Contains(ThisNodeInfo))
            {
                await AddAsync(ThisNodeInfo, ThisNodeInfo.Id);
            }

            return result;
        }

        public static readonly IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto> DefaultFactory = new Factory();

        private sealed class Factory : IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto>
        {
            public NodeSet Create(OrSetDto orSetDto, string instanceId) => new (orSetDto, instanceId);
        }

        // /// <inheritdoc />
        // public override string TypeName => nameof(NodeSet);
    }
}