using MessagePack;
using MessagePack.Formatters;
using Nyris.Crdt.Distributed.Model;
using Nyris.ManagedCrdtsV2;
using ShardId = Nyris.ManagedCrdtsV2.ShardId;

namespace Nyris.Crdt.Serialization.MessagePack.Formatters;

public sealed class GlobalShardIdFormatter : IMessagePackFormatter<GlobalShardId>
{
    public void Serialize(ref MessagePackWriter writer, GlobalShardId value, MessagePackSerializerOptions options)
    {
        writer.Write(value.InstanceId.AsReadOnlySpan);
        writer.Write(value.ShardId.AsUint);
    }

    public GlobalShardId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var instanceId = InstanceId.FromChars(reader.ReadString());
        var shardId = ShardId.FromUint(reader.ReadUInt32());
        return new GlobalShardId(instanceId, shardId);
    }
}