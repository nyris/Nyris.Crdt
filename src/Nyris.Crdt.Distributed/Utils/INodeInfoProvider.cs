using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils;

public interface INodeInfoProvider
{
    NodeId ThisNodeId { get; }
    NodeInfo GetMyNodeInfo();
}
