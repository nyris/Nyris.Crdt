using System;
using Nyris.Crdt.Distributed.Crdts.Operations;

namespace Nyris.Crdt.AspNetExample
{
    public sealed record DeleteImageOperation(ImageGuid Key, DateTime DateTime) : GetValueOperation<ImageGuid>(Key);
}