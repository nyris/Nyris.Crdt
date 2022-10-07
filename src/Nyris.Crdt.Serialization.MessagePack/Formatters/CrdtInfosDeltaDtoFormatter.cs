using Nyris.Crdt.Distributed.Model;
using Nyris.ManagedCrdtsV2;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class CrdtInfosDeltaDtoFormatter 
    : ObservedRemoveMapDeltaDtoFormatter<NodeId, GlobalShardId, CrdtInfo, CrdtInfo.DeltaDto, CrdtInfo.CausalTimestamp> {}