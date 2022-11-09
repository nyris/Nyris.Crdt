using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class GlobalShardIdFormatter : IMessagePackFormatter<ReplicaId>
{
    public void Serialize(ref MessagePackWriter writer, ReplicaId value, MessagePackSerializerOptions options)
    {
        writer.Write(value.InstanceId.ToString());
        writer.Write(value.ShardId.AsUint);
    }

    public ReplicaId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var instanceId = InstanceId.FromString(reader.ReadString());
        var shardId = ShardId.FromUint(reader.ReadUInt32());
        return new ReplicaId(instanceId, shardId);
    }
}