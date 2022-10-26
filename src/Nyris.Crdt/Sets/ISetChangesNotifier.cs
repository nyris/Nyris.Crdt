namespace Nyris.Crdt.Sets
{
    public interface ISetChangesNotifier<out TItem>
    {
        void SubscribeToChanges(ISetObserver<TItem> observer);
    }
}
