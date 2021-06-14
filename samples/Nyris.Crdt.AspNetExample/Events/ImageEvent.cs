using System;

namespace Nyris.Crdt.AspNetExample.Events
{
    internal abstract record ImageEvent(Guid ImageUuid, Guid IndexId);

    internal sealed record ImageDataSetEvent(Guid ImageUuid, Guid IndexId, string ImageId, Uri DownloadUri)
        : ImageEvent(ImageUuid, IndexId);

    internal sealed record ImageDeletedEvent(Guid ImageUuid, Guid IndexId) : ImageEvent(ImageUuid, IndexId);
}