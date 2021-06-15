using System;

namespace Nyris.Crdt.AspNetExample.Events
{
    internal sealed record ImageDeletedEvent(Guid ImageUuid, Guid IndexId) : ImageEvent(ImageUuid, IndexId);
}
