using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.Metadata;

public sealed record CrdtInfoNodesWithReplicaDelta(ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, NodeId>.DeltaDto> Delta) : CrdtInfoDelta;