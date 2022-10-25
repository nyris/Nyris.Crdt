using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Model.Deltas;

namespace Nyris.Crdt.Managed.Metadata;

internal sealed class CrdtConfigs : ObservedRemoveMap<NodeId, InstanceId, CrdtConfig, CrdtConfigDelta, CrdtConfigCausalTimestamp>{}