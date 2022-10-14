using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.Metadata;

public sealed record CrdtInfoCausalTimestamp(DateTime StorageSize, OptimizedObservedRemoveSetV2<NodeId, NodeId>.CausalTimestamp Nodes);