using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Model.Deltas;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public class CrdtInfoDeltaDtoFormatter : IMessagePackFormatter<CrdtInfoDelta>
{
    public void Serialize(ref MessagePackWriter writer, CrdtInfoDelta value, MessagePackSerializerOptions options)
    {
        switch (value)
        {
            case CrdtInfoNodesWithReplicaDelta nodesWithReplicaDto:
                writer.Write(true);
                var deltaFormatter = options.Resolver
                    .GetFormatterWithVerify<ImmutableArray<ObservedRemoveDtos<NodeId, NodeId>.DeltaDto>>();
                deltaFormatter.Serialize(ref writer, nodesWithReplicaDto.Delta, options);
                break;
            case CrdtInfoStorageSizeDelta storageSizeDto:
                writer.Write(false);
                writer.WriteUInt64(storageSizeDto.Value);
                writer.Write(storageSizeDto.DateTime);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public CrdtInfoDelta Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        switch (reader.ReadBoolean())
        {
            case true:
                var deltaFormatter = options.Resolver
                    .GetFormatterWithVerify<ImmutableArray<ObservedRemoveDtos<NodeId, NodeId>.DeltaDto>>();
                var deltas = deltaFormatter.Deserialize(ref reader, options);
                return new CrdtInfoNodesWithReplicaDelta(deltas);
            case false:
                return new CrdtInfoStorageSizeDelta(reader.ReadUInt64(), reader.ReadDateTime());
        }
    }
}