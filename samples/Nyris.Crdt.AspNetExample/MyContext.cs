using Nyris.Crdt.Distributed;

namespace Nyris.Crdt.AspNetExample
{
    internal sealed class MyContext : ManagedCrdtContext
    {
        public MyContext()
        {
            Add(Set1, ManagedGrowthSet.DefaultFactory);
            Add(Set2, ManagedGrowthSet.DefaultFactory);
            Add(Registry, IntsRegistry.DefaultFactory);
        }

        public IntsRegistry Registry { get; } = new("whatever");
        public ManagedGrowthSet Set1 { get; } = new("0");
        public ManagedGrowthSet Set2 { get; } = new("aaa");
    }
}