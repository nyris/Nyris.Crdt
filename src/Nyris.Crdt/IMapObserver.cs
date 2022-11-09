namespace Nyris.Crdt
{
    public interface IMapObserver<in TKey, in TValue>
    {
        void ElementAdded(TKey key, TValue value);
        void ElementUpdated(TKey key, TValue newValue);
        void ElementRemoved(TKey key);
    }
}
