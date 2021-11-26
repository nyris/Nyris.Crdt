using System.Collections.Generic;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>
    {
        private static readonly NodeInfo ThisNodeInfo = NodeInfoProvider.GetMyNodeInfo();

        /// <inheritdoc />
        public NodeSet(string id) : base(id)
        {
        }

        private NodeSet(WithId<OrSetDto> orSetDto) : base(orSetDto)
        {
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo> other)
        {
            var result = await base.MergeAsync(other);

            if (!Value.Contains(ThisNodeInfo))
            {
                await AddAsync(ThisNodeInfo, ThisNodeInfo.Id);
            }

            return result;
        }

        public static readonly IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto> DefaultFactory = new Factory();

        private sealed class Factory : IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto>
        {
            public NodeSet Create(WithId<OrSetDto> orSetDto) => new (orSetDto);
        }

        /// <inheritdoc />
        public override string TypeName => nameof(NodeSet);
    }
}