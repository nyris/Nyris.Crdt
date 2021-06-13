namespace Nyris.Crdt.Distributed
{
    internal interface ICreateManagedCrdtsInside
    {
        ManagedCrdtContext ManagedCrdtContext { set; }
    }
}