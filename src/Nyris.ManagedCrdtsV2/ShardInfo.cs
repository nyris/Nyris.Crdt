namespace Nyris.ManagedCrdtsV2;

public readonly struct ShardInfo
{
    public readonly ulong Size;
    public readonly GlobalShardId ShardId;
    public readonly uint NumberOfDesiredReplicas;

    public ShardInfo(GlobalShardId shardId, ulong size, uint numberOfDesiredReplicas)
    {
        Size = size;
        ShardId = shardId;
        NumberOfDesiredReplicas = numberOfDesiredReplicas;
    }
}