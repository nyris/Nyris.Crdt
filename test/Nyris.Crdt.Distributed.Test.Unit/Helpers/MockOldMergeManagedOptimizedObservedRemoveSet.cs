using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using System.Collections.Generic;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers;

internal class MockOldMergeManagedOptimizedObservedRemoveSet : OldMergeManagedOptimizedObservedRemoveSet<NodeId,
    MockUser, MockOldMergeManagedOptimizedObservedRemoveSet.MockUserSetDto>
{
    public MockOldMergeManagedOptimizedObservedRemoveSet(InstanceId id, IAsyncQueueProvider? queueProvider = null,
        ILogger? logger = null) : base(id, queueProvider, logger) { }

    [ProtoContract]
    public class MockUserSetDto : OrSetDto
    {
        [ProtoMember(1)]
        public override HashSet<DottedItemWithActor<NodeId, MockUser>>? Items { get; set; }

        [ProtoMember(2)]
        public override Dictionary<NodeId, uint>? VersionVectors { get; set; }
    }
}
