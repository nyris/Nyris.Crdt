using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface INodeClientFactory
{
    INodeClient GetClientForNodeCandidate(Uri address);
    INodeClient GetClient(NodeInfo nodeInfo);

    void SubscribeToNodeFailures(INodeFailureObserver observer);
}