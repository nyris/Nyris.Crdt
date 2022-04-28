using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers;

internal class MockManagedOptimizedObservedRemoveSet : ManagedOptimizedObservedRemoveSet<NodeId, MockUser,
    MockManagedOptimizedObservedRemoveSet.MockUserSetDto>
{
    public MockManagedOptimizedObservedRemoveSet(InstanceId id, IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null) : base(id, queueProvider, logger)
    {
    }

    [ProtoContract]
    public sealed class MockUserSetDto : OrSetDto
    {
        [ProtoMember(1)]
        public override HashSet<DottedItem<NodeId, MockUser>>? Items { get; set; }

        [ProtoMember(2)]
        public override Dictionary<NodeId, VersionVector<NodeId>>? VersionVectors { get; set; }

        [ProtoMember(3)]
        public override HashSet<Tombstone<NodeId>>? Tombstones { get; set; }
    }
}
