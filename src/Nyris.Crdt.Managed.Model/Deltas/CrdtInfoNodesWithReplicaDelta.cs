using System.Collections.Immutable;

namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtInfoNodesWithReplicaDelta(ImmutableArray<OptimizedObservedRemoveCore<NodeId, NodeId>.DeltaDto> Delta) : CrdtInfoDelta;