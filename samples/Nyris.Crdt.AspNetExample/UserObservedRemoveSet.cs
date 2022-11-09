using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Metrics;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.AspNetExample;

public sealed class
    UserObservedRemoveSet : ManagedOptimizedObservedRemoveSet<NodeId, User, UserObservedRemoveSet.UserSetDto>
{
    public UserObservedRemoveSet(InstanceId id, NodeInfo nodeInfo,
        ICrdtMetricsRegistry? metricsRegistry = null,
        IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null) :
        base(id, nodeInfo.Id, queueProvider, logger, metricsRegistry) { }

    [ProtoContract]
    public sealed class UserSetDto : OrSetDto
    {
        [ProtoMember(1)]
        public override HashSet<DottedItemWithActor<NodeId, User>>? Items { get; set; }

        [ProtoMember(2)]
        public override Dictionary<NodeId, uint>? VersionVectors { get; set; }

        [ProtoMember(3)]
        public override Dictionary<IntDot<NodeId>, HashSet<NodeId>>? Tombstones { get; set; }

        [ProtoMember(4)]
        public override NodeId SourceId { get; set; }
    }

    public sealed class Factory : INodeAwareManagedCrdtFactory<UserObservedRemoveSet, UserSetDto>
    {
        private readonly IAsyncQueueProvider? _queueProvider;
        private readonly ILogger? _logger;
        private readonly ICrdtMetricsRegistry? _metricsRegistry;

        public Factory() { }

        public Factory(IAsyncQueueProvider? queueProvider = null, ILogger? logger = null, ICrdtMetricsRegistry? metricsRegistry = null)
        {
            _queueProvider = queueProvider;
            _logger = logger;
            _metricsRegistry = metricsRegistry;
        }

        public UserObservedRemoveSet Create(InstanceId instanceId, NodeInfo nodeInfo) =>
            new(instanceId, nodeInfo, _metricsRegistry, _queueProvider, _logger);
    }
}
