using Nyris.Crdt.Distributed;

namespace Nyris.Crdt.AspNetExample
{
    internal sealed class MyContext : ManagedCrdtContext
    {
        public GrowthSet Set1 { get; } = new(0);

        public GrowthSet Set2 { get; } = new(1);

        public MyContext()
        {
            Add(Set1, GrowthSet.FactoryInstance);
            Add(Set2, GrowthSet.FactoryInstance);
        }
    }
}