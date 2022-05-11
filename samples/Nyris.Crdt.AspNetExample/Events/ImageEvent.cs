using Nyris.Crdt.AspNetExample.Mongo;
using System;

namespace Nyris.Crdt.AspNetExample.Events;

// TODO: DATA-671 Make IndexId strongly typed
public abstract record ImageEvent(Guid ImageUuid, Guid IndexId)
{
    public abstract ImageDocument ToBson(DateTime dateTime);

    public virtual bool IsValid() => ImageUuid != Guid.Empty && IndexId != Guid.Empty;
}
