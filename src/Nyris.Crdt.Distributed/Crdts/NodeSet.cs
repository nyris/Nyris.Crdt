using System.Collections.Generic;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts
{
    internal sealed class NodeSet : OptimizedObservedRemoveSet<NodeId, NodeInfo>
    {
        /// <inheritdoc />
        public NodeSet(int id) : base(id)
        {
        }

        private NodeSet(Dto dto) : base(dto)
        {
        }

        public static readonly ICRDTFactory<NodeSet, OptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, Dto> DefaultFactory = new Factory();

        private sealed class Factory : ICRDTFactory<NodeSet, OptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, Dto>
        {
            public NodeSet Create(Dto dto) => new (dto);
        }
    }
}