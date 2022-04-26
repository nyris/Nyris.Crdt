using System;
using Nyris.Crdt.AspNetExample.Mongo;

namespace Nyris.Crdt.AspNetExample.Events
{
    public sealed record ImageDataSetEvent(Guid ImageUuid, Guid IndexId, string ImageId, Uri DownloadUrl)
        : ImageEvent(ImageUuid, IndexId)
    {
        /// <inheritdoc />
        public override bool IsValid() => base.IsValid() && !string.IsNullOrWhiteSpace(ImageId) && DownloadUrl is not null;

        public override ImageDocument ToBson(DateTime dateTime) => new ImageDataSetDocument
        {
            ImageUuid = ImageUuid,
            IndexId = IndexId,
            ImageId = ImageId,
            DownloadUrl = DownloadUrl,
            EventTime = dateTime
        };
    }
}
