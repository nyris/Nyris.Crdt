namespace Nyris.Crdt.Transport.Abstractions;

public interface INodeFailureNotifier
{
    void SubscribeToNodeFailures(INodeFailureObserver observer);
}