using Nyris.Crdt;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public sealed class CrdtConfigs : ObservedRemoveMap<NodeId, InstanceId, CrdtConfig, CrdtConfig.DeltaDto, CrdtConfig.CausalTimestamp>{}