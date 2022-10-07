using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class NodeInfoSetDeltaFormatter : ObservedRemoveSetDeltaDtoFormatter<NodeId, NodeInfo>
{
}