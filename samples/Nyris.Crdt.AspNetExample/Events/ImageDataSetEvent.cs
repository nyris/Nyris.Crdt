using System;
using Nyris.Crdt.AspNetExample.Mongo;

namespace Nyris.Crdt.AspNetExample.Events
{
    internal sealed record ImageDataSetEvent(Guid ImageUuid, Guid IndexId, string ImageId, Uri DownloadUri)
        : ImageEvent(ImageUuid, IndexId)
    {
        public override ImageDocument ToBson(DateTime dateTime) => new ImageDataSetDocument
        {
            ImageUuid = ImageUuid,
            IndexId = IndexId,
            ImageId = ImageId,
            DownloadUri = DownloadUri,
            EventTime = dateTime
        };
    }
}
