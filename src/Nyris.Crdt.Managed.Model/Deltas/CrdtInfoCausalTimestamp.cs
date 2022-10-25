using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtInfoCausalTimestamp(DateTime StorageSize, ObservedRemoveDtos<NodeId, NodeId>.CausalTimestamp Nodes);