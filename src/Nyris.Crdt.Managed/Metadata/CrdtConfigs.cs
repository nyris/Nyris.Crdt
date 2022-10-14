using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Metadata;

internal sealed class CrdtConfigs : ObservedRemoveMap<NodeId, InstanceId, CrdtConfig, CrdtConfigDelta, CrdtConfigCausalTimestamp>{}