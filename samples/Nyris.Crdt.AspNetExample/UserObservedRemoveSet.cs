using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class
        UserObservedRemoveSet : ManagedOptimizedObservedRemoveSet<NodeId, User, UserObservedRemoveSet.UserSetDto>
    {
        public UserObservedRemoveSet(InstanceId id, IAsyncQueueProvider? queueProvider = null, ILogger? logger = null) :
            base(id, queueProvider, logger)
        {
        }

        [ProtoContract]
        public sealed class UserSetDto : OrSetDto
        {
            [ProtoMember(1)]
            public override HashSet<DottedItem<NodeId, User>>? Items { get; set; }

            [ProtoMember(2)]
            public override Dictionary<NodeId, VersionVector<NodeId>>? VersionVectors { get; set; }

            [ProtoMember(3)]
            public override HashSet<Tombstone<NodeId>>? Tombstones { get; set; }
        }

        public sealed class Factory : IManagedCRDTFactory<UserObservedRemoveSet, UserSetDto>
        {
            private readonly IAsyncQueueProvider? _queueProvider;
            private readonly ILogger? _logger;

            public Factory()
            {
            }

            public Factory(IAsyncQueueProvider? queueProvider = null, ILogger? logger = null)
            {
                _queueProvider = queueProvider;
                _logger = logger;
            }

            public UserObservedRemoveSet Create(InstanceId instanceId) => new(instanceId, _queueProvider, _logger);
        }
    }
}
