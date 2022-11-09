using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface INodeFailureObserver
{
    Task NodeFailureObservedAsync(NodeId nodeId, CancellationToken cancellationToken = default);
}