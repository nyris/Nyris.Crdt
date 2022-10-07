using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2;

public sealed class NodeInfoSet : OptimizedObservedRemoveSetV2<NodeId, NodeInfo> {}