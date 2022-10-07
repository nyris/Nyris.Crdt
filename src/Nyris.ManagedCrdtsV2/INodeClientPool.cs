using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface INodeClientPool
{
    INodeClient GetClientForNodeCandidate(Uri address);
    INodeClient GetClient(NodeInfo nodeInfo);

    void SubscribeToNodeFailures(INodeFailureObserver observer);
}