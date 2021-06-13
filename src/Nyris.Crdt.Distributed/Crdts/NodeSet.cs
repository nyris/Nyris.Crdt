using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>
    {
        /// <inheritdoc />
        public NodeSet(string id) : base(id)
        {
        }

        private NodeSet(OrSetDto orSetDto) : base(orSetDto)
        {
        }

        public static readonly IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto> DefaultFactory = new Factory();

        private sealed class Factory : IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto>
        {
            public NodeSet Create(OrSetDto orSetDto) => new (orSetDto);
        }

        /// <inheritdoc />
        public override string TypeName { get; } = nameof(NodeSet);
    }
}