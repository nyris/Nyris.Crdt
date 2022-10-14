using System.Collections.Immutable;
using System.Diagnostics;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class NodeInfoSetDtoFormatter : IMessagePackFormatter<OptimizedObservedRemoveSetV2<NodeId, NodeInfo>.Dto>
{
    public void Serialize(ref MessagePackWriter writer, OptimizedObservedRemoveSetV2<NodeId, NodeInfo>.Dto value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        
        var versionContextFormatter = options.Resolver
            .GetFormatterWithVerify<ImmutableDictionary<NodeId, ImmutableArray<Range>>>();
        var itemsFormatter = options.Resolver
            .GetFormatterWithVerify<ImmutableDictionary<NodeId, ImmutableDictionary<NodeInfo, ulong>>>();
        
        versionContextFormatter.Serialize(ref writer, value.VersionContext, options);
        itemsFormatter.Serialize(ref writer, value.Items, options);
    }

    public OptimizedObservedRemoveSetV2<NodeId, NodeInfo>.Dto Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        Debug.Assert(count == 2);

        var versionContextFormatter = options.Resolver
            .GetFormatterWithVerify<ImmutableDictionary<NodeId, ImmutableArray<Range>>>();
        var itemsFormatter = options.Resolver
            .GetFormatterWithVerify<ImmutableDictionary<NodeId, ImmutableDictionary<NodeInfo, ulong>>>();

        var versionContext = versionContextFormatter.Deserialize(ref reader, options);
        var items = itemsFormatter.Deserialize(ref reader, options);
        return new OptimizedObservedRemoveSetV2<NodeId, NodeInfo>.Dto(versionContext, items);
    }
}