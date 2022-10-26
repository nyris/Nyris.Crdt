using System.Collections.Immutable;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtInfoNodesWithReplicaDelta(ImmutableArray<ObservedRemoveDtos<NodeId, NodeId>.DeltaDto> Delta) : CrdtInfoDelta;