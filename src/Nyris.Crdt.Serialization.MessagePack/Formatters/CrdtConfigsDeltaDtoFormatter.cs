using Nyris.Crdt.Distributed.Model;
using Nyris.ManagedCrdtsV2;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class CrdtConfigsDeltaDtoFormatter 
    : ObservedRemoveMapDeltaDtoFormatter<NodeId, InstanceId, CrdtConfig, CrdtConfig.DeltaDto, CrdtConfig.CausalTimestamp> {}