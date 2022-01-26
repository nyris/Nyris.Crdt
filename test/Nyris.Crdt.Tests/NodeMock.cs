using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Tests;

record NodeMock(NodeId Id, ManagedCrdtContext Context, INodeInfoProvider InfoProvider);