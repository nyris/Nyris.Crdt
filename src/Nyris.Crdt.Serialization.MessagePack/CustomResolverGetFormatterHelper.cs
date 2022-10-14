using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Serialization.MessagePack.Formatters;
using Nyris.Crdt.Sets;
using Nyris.ManagedCrdtsV2;
using Nyris.ManagedCrdtsV2.Metadata;
using InstanceId = Nyris.Crdt.Managed.Model.InstanceId;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Serialization.MessagePack;

internal static class CustomResolverGetFormatterHelper
{
    // If type is concrete type, use type-formatter map
    private static readonly Dictionary<Type, object> FormatterMap = new()
    {
        {typeof(NodeInfoSet.Dto), new NodeInfoSetDtoFormatter()},
        {typeof(NodeId), NodeIdFormatter.Instance},
        {typeof(Range), new RangeFormatter()},
        {typeof(NodeInfo), new NodeInfoFormatter()},
        {typeof(CrdtInfo.DeltaDto), new CrdtInfoDeltaDtoFormatter()},
        {typeof(CrdtConfig.DeltaDto), new CrdtConfigDeltaDtoFormatter()},
        {typeof(ReplicaId), new GlobalShardIdFormatter()},
        {typeof(InstanceId), new InstanceIdFormatter()}
    };
    
    private static readonly Dictionary<Type, Type> GenericFormatters = new()
    {
        [typeof(OptimizedObservedRemoveSetV2<,>.DeltaDto)] = typeof(ObservedRemoveSetDeltaDtoFormatter<,>),
        [typeof(ObservedRemoveMap<,,,,>.DeltaDto)] = typeof(ObservedRemoveMapDeltaDtoFormatter<,,,,>),
        [typeof(OptimizedObservedRemoveSetV2<,>.CausalTimestamp)] = typeof(ObservedRemoveSetCausalTimestampFormatter<,>),
        [typeof(ObservedRemoveMap<,,,,>.CausalTimestamp)] = typeof(ObservedRemoveMapCausalTimestampFormatter<,,,,>),
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