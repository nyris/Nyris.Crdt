using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Model.Deltas;
using Nyris.Crdt.Serialization.MessagePack.Formatters;
using Nyris.Crdt.Sets;
using InstanceId = Nyris.Crdt.Managed.Model.InstanceId;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Serialization.MessagePack;


public static class CustomResolverGetFormatterHelper
{
    // If type is concrete type, use type-formatter map
    public static readonly Dictionary<Type, object> FormatterMap = new()
    {
        {typeof(ObservedRemoveDtos<NodeId, NodeInfo>.Dto), new NodeInfoSetDtoFormatter()},
        {typeof(NodeId), NodeIdFormatter.Instance},
        {typeof(Range), new RangeFormatter()},
        {typeof(NodeInfo), new NodeInfoFormatter()},
        {typeof(CrdtInfoDelta), new CrdtInfoDeltaDtoFormatter()},
        {typeof(CrdtConfigDelta), new CrdtConfigDeltaDtoFormatter()},
        {typeof(ReplicaId), new GlobalShardIdFormatter()},
        {typeof(InstanceId), new InstanceIdFormatter()}
    };

    private static readonly Dictionary<Type, Type> GenericFormatters = new()
    {
        [typeof(ObservedRemoveDtos<,>.DeltaDto)] = typeof(ObservedRemoveSetDeltaDtoFormatter<,>),
        [typeof(ObservedRemoveDtos<,>.CausalTimestamp)] = typeof(ObservedRemoveSetCausalTimestampFormatter<,>),
        [typeof(ObservedRemoveMap<,,,,>.DeltaDto)] = typeof(ObservedRemoveMapDeltaDtoFormatter<,,,,>),
        [typeof(ObservedRemoveMap<,,,,>.CausalTimestamp)] = typeof(ObservedRemoveMapCausalTimestampFormatter<,,,,>),
        [typeof(ObservedRemoveCore<,>.DeltaDto)] = typeof(ObservedRemoveCoreDeltaDtoFormatter<,>),
        [typeof(ObservedRemoveCore<,>.CausalTimestamp)] = typeof(ObservedRemoveCoreCausalTimestampFormatter<,>),
        [typeof(ObservedRemoveMapV2<,,,,>.MapDeltaItem)] = typeof(MapDeltaItemFormatter<,,,,>)
    };

    internal static object? GetFormatter(Type t)
    {
        if (FormatterMap.TryGetValue(t, out var formatter))
        {
            return formatter;
        }

        if (t.IsGenericType && GenericFormatters.TryGetValue(t.GetGenericTypeDefinition(), out var formatterType))
        {
            return Activator.CreateInstance(formatterType.MakeGenericType(t.GetGenericArguments()));
        }

        return null;
    }
}
