using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Serialization.MessagePack.Formatters;
using Nyris.Crdt.Sets;
using Nyris.ManagedCrdtsV2;
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
        {typeof(NodeInfoSet.DeltaDto), new NodeInfoSetDeltaFormatter()},
        {typeof(CrdtInfo.DeltaDto), new CrdtInfoDeltaDtoFormatter()},
        {typeof(CrdtConfig.DeltaDto), new CrdtConfigDeltaDtoFormatter()},
        {typeof(GlobalShardId), new GlobalShardIdFormatter()},
        {typeof(CrdtConfigs.DeltaDto), new CrdtConfigsDeltaDtoFormatter()},
        {typeof(CrdtInfos.DeltaDto), new CrdtInfosDeltaDtoFormatter()},
        {typeof(InstanceId), new InstanceIdFormatter()}
    };
    
    private static readonly Dictionary<Type, Type> GenericFormatters = new()
    {
        [typeof(OptimizedObservedRemoveSetV2<,>.DeltaDto)] = typeof(ObservedRemoveSetDeltaDtoFormatter<,>),
        [typeof(ObservedRemoveMap<,,,,>.DeltaDto)] = typeof(ObservedRemoveMapDeltaDtoFormatter<,,,,>)
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