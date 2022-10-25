namespace Nyris.Crdt.Sets
{
    public interface ISetChangesNotifier<out TItem>
    {
        void SubscribeToChanges(ISetChangeObserver<TItem> observer);
    }
}
