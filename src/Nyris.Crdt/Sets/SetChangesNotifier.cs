using System;

namespace Nyris.Crdt.Sets
{
    public abstract class SetChangesNotifier<TItem> : ISetChangesNotifier<TItem>
    {
        // keep everything in array for slightly faster indexing - it is assumed that adding observers
        // is a very rare operation, while notifications happen all the time
        private ISetObserver<TItem>[] _observers = Array.Empty<ISetObserver<TItem>>();
        private readonly object _lock = new();

        public void SubscribeToChanges(ISetObserver<TItem> observer)
        {
            lock(_lock)
            {
                var newArray = new ISetObserver<TItem>[_observers.Length + 1];
                Array.Copy(_observers, newArray, _observers.Length);
                newArray[_observers.Length] = observer;
                _observers = newArray;
            }
        }

        protected void NotifyAdded(in TItem item)
        {
            foreach (var observer in _observers) observer.ElementAdded(item);
        }

        protected void NotifyRemoved(in TItem item)
        {
            foreach (var observer in _observers) observer.ElementRemoved(item);
        }
    }
}
