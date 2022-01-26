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
    public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeSet, NodeId, NodeInfo>
    {
        private readonly NodeInfo _thisNodeInfo;
        private readonly ConcurrentBag<IRebalanceAtNodeChange> _needRebalancing = new();

        /// <inheritdoc />
        public NodeSet(string id, INodeInfoProvider? nodeInfoProvider = null) : base(id)
        {
            _thisNodeInfo = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
        }

        private NodeSet(OrSetDto orSetDto, string instanceId, INodeInfoProvider? nodeInfoProvider = null)
            : base(orSetDto, instanceId)
        {
            _thisNodeInfo = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
        }

        internal void RebalanceOnChange(IRebalanceAtNodeChange element) => _needRebalancing.Add(element);

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(NodeSet other, CancellationToken cancellationToken = default)
        {
            var result = await base.MergeAsync(other, cancellationToken);

            if (!Value.Contains(_thisNodeInfo))
            {
                await AddAsync(_thisNodeInfo, _thisNodeInfo.Id);
            }

            if(result == MergeResult.ConflictSolved) await RebalanceAllAsync();
            return result;
        }

        /// <inheritdoc />
        public override async Task AddAsync(NodeInfo item, NodeId actorPerformingAddition)
        {
            await base.AddAsync(item, actorPerformingAddition);
            await RebalanceAllAsync();
        }

        /// <inheritdoc />
        public override async Task RemoveAsync(NodeInfo item)
        {
            await base.RemoveAsync(item);
            await RebalanceAllAsync();
        }

        /// <inheritdoc />
        public override async Task RemoveAsync(Func<NodeInfo, bool> condition)
        {
            await base.RemoveAsync(condition);
            await RebalanceAllAsync();
        }

        private async Task RebalanceAllAsync()
        {
            foreach (var element in _needRebalancing)
            {
                await element.RebalanceAsync();
            }
        }

        public static readonly IManagedCRDTFactory<NodeSet, HashSet<NodeInfo>, OrSetDto> DefaultFactory = new Factory();

        private sealed class Factory : IManagedCRDTFactory<NodeSet, HashSet<NodeInfo>, OrSetDto>
        {
            public NodeSet Create(OrSetDto orSetDto, string instanceId) => new (orSetDto, instanceId);

            /// <inheritdoc />
            public NodeSet Create(string instanceId) => new(instanceId);
        }
    }
}