namespace Nyris.Crdt.Sets
{
    public interface ISetObserver<in TItem>
    {
        void ElementAdded(TItem item);
        void ElementRemoved(TItem item);
    }
}
