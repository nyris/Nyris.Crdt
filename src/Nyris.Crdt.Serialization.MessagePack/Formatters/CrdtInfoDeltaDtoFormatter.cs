using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;
using Nyris.ManagedCrdtsV2;
using Nyris.ManagedCrdtsV2.Metadata;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class CrdtInfoDeltaDtoFormatter : IMessagePackFormatter<CrdtInfo.DeltaDto>
{
    public void Serialize(ref MessagePackWriter writer, CrdtInfo.DeltaDto value, MessagePackSerializerOptions options)
    {
        switch (value)
        {
            case CrdtInfo.NodesWithReplicaDto nodesWithReplicaDto:
                writer.Write(true);
                var deltaFormatter = options.Resolver
                    .GetFormatterWithVerify<ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, NodeId>.DeltaDto>>();
                deltaFormatter.Serialize(ref writer, nodesWithReplicaDto.Delta, options);
                break;
            case CrdtInfo.StorageSizeDto storageSizeDto:
                writer.Write(false);
                writer.WriteUInt64(storageSizeDto.Value);
                writer.Write(storageSizeDto.DateTime);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public CrdtInfo.DeltaDto Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        switch (reader.ReadBoolean())
        {
            case true:
                var deltaFormatter = options.Resolver
                    .GetFormatterWithVerify<ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, NodeId>.DeltaDto>>();
                var deltas = deltaFormatter.Deserialize(ref reader, options);
                return new CrdtInfo.NodesWithReplicaDto(deltas);
            case false:
                return new CrdtInfo.StorageSizeDto(reader.ReadUInt64(), reader.ReadDateTime());
        }
    }
}