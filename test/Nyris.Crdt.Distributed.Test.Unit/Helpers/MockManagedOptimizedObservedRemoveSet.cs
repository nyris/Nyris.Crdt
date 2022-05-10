using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers;

internal class MockManagedOptimizedObservedRemoveSet : ManagedOptimizedObservedRemoveSet<NodeId, MockUser,
    MockManagedOptimizedObservedRemoveSet.MockUserSetDto>
{
    public MockManagedOptimizedObservedRemoveSet(InstanceId id, NodeId nodeId,
        IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null) : base(id, nodeId, queueProvider, logger) { }

    [ProtoContract]
    public sealed class MockUserSetDto : OrSetDto
    {
        [ProtoMember(1)]
        public override HashSet<DottedItem<NodeId, MockUser>>? Items { get; set; }

        [ProtoMember(2)]
        public override Dictionary<NodeId, uint>? VersionVectors { get; set; }

        [ProtoMember(3)]
        public override Dictionary<Dot<NodeId>, HashSet<NodeId>>? Tombstones { get; set; }

        [ProtoMember(4)]
        public override NodeId? SourceId { get; set; }
    }
}
