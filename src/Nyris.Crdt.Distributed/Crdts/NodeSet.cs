using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class NodeSet : OptimizedObservedRemoveSet<NodeId, NodeInfo>
    {
        /// <inheritdoc />
        public NodeSet(string id) : base(id)
        {
        }

        private NodeSet(Dto dto) : base(dto)
        {
        }

        public static readonly IAsyncCRDTFactory<NodeSet, OptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, Dto> DefaultFactory = new Factory();

        private sealed class Factory : IAsyncCRDTFactory<NodeSet, OptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, Dto>
        {
            public NodeSet Create(Dto dto) => new (dto);
        }

        /// <inheritdoc />
        public override string TypeName { get; } = nameof(NodeSet);
    }
}