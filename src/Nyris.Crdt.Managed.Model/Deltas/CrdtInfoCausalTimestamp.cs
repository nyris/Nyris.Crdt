namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtInfoCausalTimestamp(DateTime StorageSize, OptimizedObservedRemoveCore<NodeId, NodeId>.CausalTimestamp Nodes);