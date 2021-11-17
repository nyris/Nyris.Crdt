using Nyris.Crdt.Distributed;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class MyContext : ManagedCrdtContext
    {
        public MyContext()
        {
            Add(ImageCollectionsRegistry, ItemInfoCollectionsRegistry.DefaultFactory);
        }

        public ItemInfoCollectionsRegistry ImageCollectionsRegistry { get; } = new("whatever");
    }
}