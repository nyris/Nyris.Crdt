using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Services;

public interface INodeInfoProvider
{
    NodeId ThisNodeId { get; }
    NodeInfo GetMyNodeInfo();
}
