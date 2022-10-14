using Nyris.Crdt;
using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Metadata;

public sealed class CrdtConfigs : ObservedRemoveMap<NodeId, InstanceId, CrdtConfig, CrdtConfig.DeltaDto, CrdtConfig.CausalTimestamp>{}