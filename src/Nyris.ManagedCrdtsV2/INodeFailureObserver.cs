using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface INodeFailureObserver
{
    Task NodeFailureObservedAsync(NodeId nodeId, CancellationToken cancellationToken = default);
}