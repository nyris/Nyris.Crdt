using System;
using Nyris.Crdt.Distributed.Crdts.Operations;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record DeleteImageOperation([property: ProtoMember(1)] ImageGuid Key,
        [property: ProtoMember(2)] DateTime DateTime,
        [property: ProtoMember(3)] uint PropagateToNodes) : OperationWithKey<ImageGuid>;
}