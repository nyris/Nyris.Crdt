using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Services;

public interface INodeInfoProvider
{
    NodeId ThisNodeId { get; }
    NodeInfo GetMyNodeInfo();
}
