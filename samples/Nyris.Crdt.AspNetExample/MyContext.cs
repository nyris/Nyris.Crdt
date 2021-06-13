using Nyris.Crdt.Distributed;

namespace Nyris.Crdt.AspNetExample
{
    internal sealed class MyContext : ManagedCrdtContext
    {
        public MyContext()
        {
            Add(Set1, GrowthSet.DefaultFactory);
            Add(Set2, GrowthSet.DefaultFactory);
        }

        public GrowthSet Set1 { get; } = new("0");
        public GrowthSet Set2 { get; } = new("aaa");
    }
}