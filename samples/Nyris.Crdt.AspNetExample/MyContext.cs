using Nyris.Crdt.Distributed;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class MyContext : ManagedCrdtContext
    {
        public MyContext()
        {
            Add(ImageCollectionsRegistry, ImageInfoCollectionsRegistry.DefaultFactory);
            Add(PartiallyReplicatedImageCollectionsRegistry, PartiallyReplicatedImageInfoCollectionsRegistry.DefaultFactory);
        }

        public ImageInfoCollectionsRegistry ImageCollectionsRegistry { get; } = new("sample-items-collections-registry");

        public PartiallyReplicatedImageInfoCollectionsRegistry PartiallyReplicatedImageCollectionsRegistry { get; } =
            new("partially-replicated-collection-registry");
    }
}