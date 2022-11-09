namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtInfoCausalTimestamp(DateTime StorageSize, ObservedRemoveCore<NodeId, NodeId>.CausalTimestamp Nodes);