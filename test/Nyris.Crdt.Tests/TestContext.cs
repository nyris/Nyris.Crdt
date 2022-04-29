using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Tests;

internal sealed class TestContext : ManagedCrdtContext
{
    public TestContext(NodeInfo nodeInfo, NodeSet? nodes = null) : base(nodeInfo, nodes: nodes) { }
}
