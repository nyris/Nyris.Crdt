namespace Nyris.ManagedCrdtsV2;

public interface INodeFailureNotifier
{
    void SubscribeToNodeFailures(INodeFailureObserver observer);
}