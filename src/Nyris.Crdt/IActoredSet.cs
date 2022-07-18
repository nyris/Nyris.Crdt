using System.Collections.Generic;

namespace Nyris.Crdt
{
    public interface IActoredSet<in TActorId, TItem>
    {
        HashSet<TItem> Values { get; }
        void Add(TItem item, TActorId actor);
        void Remove(TItem item);
    }
}