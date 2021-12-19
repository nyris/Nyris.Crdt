using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static readonly NodeInfo ThisNodeInfo = NodeInfoProvider.GetMyNodeInfo();
        private readonly ConcurrentBag<IRebalanceAtNodeChange> _needRebalancing = new();

        /// <inheritdoc />
        public NodeSet(string id) : base(id)
        {
        }

        private NodeSet(OrSetDto orSetDto, string instanceId) : base(orSetDto, instanceId)
        {
        }

        internal void RebalanceOnChange(IRebalanceAtNodeChange element) => _needRebalancing.Add(element);

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo> other,
            CancellationToken cancellationToken = default)
        {
            var result = await base.MergeAsync(other, cancellationToken);

            if (!Value.Contains(ThisNodeInfo))
            {
                await AddAsync(ThisNodeInfo, ThisNodeInfo.Id);
            }

            if(result == MergeResult.ConflictSolved) RebalanceAll();
            return result;
        }

        /// <inheritdoc />
        public override async Task AddAsync(NodeInfo item, NodeId actorPerformingAddition)
        {
            await base.AddAsync(item, actorPerformingAddition);
            RebalanceAll();
        }

        /// <inheritdoc />
        public override async Task RemoveAsync(NodeInfo item)
        {
            await base.RemoveAsync(item);
            RebalanceAll();
        }

        /// <inheritdoc />
        public override async Task RemoveAsync(Func<NodeInfo, bool> condition)
        {
            await base.RemoveAsync(condition);
            RebalanceAll();
        }

        private void RebalanceAll()
        {
            foreach (var element in _needRebalancing)
            {
                element.Rebalance();
            }
        }

        public static readonly IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto> DefaultFactory = new Factory();

        private sealed class Factory : IManagedCRDTFactory<NodeSet, ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo>, HashSet<NodeInfo>, OrSetDto>
        {
            public NodeSet Create(OrSetDto orSetDto, string instanceId) => new (orSetDto, instanceId);

            /// <inheritdoc />
            public NodeSet Create(string instanceId) => new(instanceId);
        }
    }
}