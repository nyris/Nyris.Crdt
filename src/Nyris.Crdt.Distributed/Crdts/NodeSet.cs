using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo, NodeSet.NodeSetDto>
    {
        private readonly NodeInfo _thisNodeInfo;

        /// <inheritdoc />
        public NodeSet(InstanceId id,
            INodeInfoProvider? nodeInfoProvider = null,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
            _thisNodeInfo = (nodeInfoProvider ?? DefaultConfiguration.NodeInfoProvider).GetMyNodeInfo();
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(NodeSetDto other, CancellationToken cancellationToken = default)
        {
            var result = await base.MergeAsync(other, cancellationToken);

            if (!Value.Contains(_thisNodeInfo))
            {
                await AddAsync(_thisNodeInfo, _thisNodeInfo.Id);
            }

            return result;
        }

        public static readonly IManagedCRDTFactory<NodeSet, NodeSetDto> DefaultFactory = new Factory();


        [ProtoContract]
        public sealed class NodeSetDto : OrSetDto
        {
            [ProtoMember(1)]
            public override HashSet<VersionedSignedItem<NodeId, NodeInfo>>? Items { get; set; }

            [ProtoMember(2)]
            public override Dictionary<NodeId, uint>? ObservedState { get; set; }
        }

        private sealed class Factory : IManagedCRDTFactory<NodeSet, NodeSetDto>
        {
            /// <inheritdoc />
            public NodeSet Create(InstanceId instanceId) => new(instanceId);
        }
    }
}