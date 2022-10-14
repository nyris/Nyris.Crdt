using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2.Metadata;

public sealed class NodeInfoSet : OptimizedObservedRemoveSetV2<NodeId, NodeInfo> {}