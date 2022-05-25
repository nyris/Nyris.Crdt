using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Metrics;

namespace Nyris.Crdt.Distributed.Crdts;

public sealed class NodeSet : ManagedOptimizedObservedRemoveSet<NodeId, NodeInfo, NodeSet.NodeSetDto>
{
    private readonly NodeInfo _thisNodeInfo;

    /// <inheritdoc />
    public NodeSet(
        InstanceId id,
        NodeInfo nodeInfo,
        IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null,
        ICrdtMetricsRegistry? metricsRegistry = null
    ) : base(id, nodeInfo.Id, queueProvider: queueProvider, logger: logger, metricsRegistry: metricsRegistry)
    {
        _thisNodeInfo = nodeInfo;
    }

    /// <inheritdoc />
    public override async Task<MergeResult> MergeAsync(NodeSetDto other, CancellationToken cancellationToken = default)
    {
        var result = await base.MergeAsync(other, cancellationToken);

        if (!Value.Contains(_thisNodeInfo))
        {
            await AddAsync(_thisNodeInfo);
        }

        return result;
    }

    public static readonly INodeAwareManagedCrdtFactory<NodeSet, NodeSetDto> DefaultFactory = new Factory();

    // TODO: Sending this DTO is failing, Problem might be new complex types now like DottedItem and Tombstones
    [ProtoContract]
    public sealed class NodeSetDto : OrSetDto
    {
        [ProtoMember(1)]
        public override HashSet<DottedItem<NodeId, NodeInfo>>? Items { get; set; }

        [ProtoMember(2)]
        public override Dictionary<NodeId, uint>? VersionVectors { get; set; }

        [ProtoMember(3)]
        public override Dictionary<Dot<NodeId>, HashSet<NodeId>>? Tombstones { get; set; }

        [ProtoMember(4)]
        public override NodeId SourceId { get; set; }
    }

    private sealed class Factory : INodeAwareManagedCrdtFactory<NodeSet, NodeSetDto>
    {
        /// <inheritdoc />
        public NodeSet Create(InstanceId instanceId, NodeInfo thisNodeInfo) => new(instanceId, thisNodeInfo);
    }
}
