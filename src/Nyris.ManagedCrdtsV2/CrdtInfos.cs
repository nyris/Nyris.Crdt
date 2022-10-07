using Nyris.Crdt;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public sealed class CrdtInfos : ObservedRemoveMap<NodeId, GlobalShardId, CrdtInfo, CrdtInfo.DeltaDto, CrdtInfo.CausalTimestamp>{}