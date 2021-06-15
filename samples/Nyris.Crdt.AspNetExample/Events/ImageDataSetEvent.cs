using System;

namespace Nyris.Crdt.AspNetExample.Events
{
    internal sealed record ImageDataSetEvent(Guid ImageUuid, Guid IndexId, string ImageId, Uri DownloadUri)
        : ImageEvent(ImageUuid, IndexId);
}
