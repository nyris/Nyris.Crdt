using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.Metadata;

internal sealed class NodeInfoSet : OptimizedObservedRemoveSetV3<NodeId, NodeInfo> {}