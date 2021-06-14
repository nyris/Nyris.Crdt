using System;

namespace Nyris.Crdt.AspNetExample.Events
{
    internal abstract record ImageEvent(Guid ImageUuid, Guid IndexId)
    {
        public abstract ImageDocument ToBson(DateTime dateTime);
    }
}