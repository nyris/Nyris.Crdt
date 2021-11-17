using System;
using Nyris.Crdt.AspNetExample.Mongo;

namespace Nyris.Crdt.AspNetExample.Events
{
    public sealed record ImageDeletedEvent(Guid ImageUuid, Guid IndexId) : ImageEvent(ImageUuid, IndexId)
    {
        public override ImageDocument ToBson(DateTime dateTime) => new ImageDeletedDocument
        {
            ImageUuid = ImageUuid,
            IndexId = IndexId,
            EventTime = dateTime
        };
    }
}