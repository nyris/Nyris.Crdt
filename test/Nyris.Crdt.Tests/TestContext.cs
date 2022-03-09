using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts;

namespace Nyris.Crdt.Tests;

internal sealed class TestContext : ManagedCrdtContext
{
    public TestContext(NodeSet? nodes = null) : base(nodes: nodes)
    {
    }
}