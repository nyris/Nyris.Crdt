namespace Nyris.Crdt.Sets
{
    public interface ISetChangeObserver<in TItem>
    {
        void ElementAdded(TItem item);
        void ElementRemoved(TItem item);
    }
}
