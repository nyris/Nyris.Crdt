using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nyris.Crdt.Distributed.Crdts;

public sealed class CollectionInfo : ICRDT<CollectionInfo.CollectionInfoDto>, IHashable
{
    private readonly SingleValue<string> _name;

    public CollectionInfo(
        string name = "",
        IEnumerable<ShardId>? shardIds = null,
        IEnumerable<string>? indexes = null
    )
    {
        _name = new SingleValue<string>(name);
        Shards = new();
        if (!ReferenceEquals(shardIds, null))
        {
            foreach (var shardId in shardIds)
            {
                Shards.TryAdd(DefaultConfiguration.NodeInfoProvider.ThisNodeId, shardId, new ShardSizes(0, 0));
            }
        }

        IndexNames = new HashableOptimizedObservedRemoveSet<NodeId, string>();

        if (indexes == null) return;

        foreach (var index in indexes)
        {
            IndexNames.Add(index, DefaultConfiguration.NodeInfoProvider.ThisNodeId);
        }
    }

    private CollectionInfo(CollectionInfoDto dto)
    {
        _name = ReferenceEquals(dto.Name, null) ? new SingleValue<string>("") : new SingleValue<string>(dto.Name);

        Shards = new HashableCrdtRegistry<NodeId, ShardId, ShardSizes>();
        Shards.MaybeMerge(dto.Shards);

        IndexNames = dto.IndexNames != null
                         ? HashableOptimizedObservedRemoveSet<NodeId, string>.FromDto(dto.IndexNames)
                         : new HashableOptimizedObservedRemoveSet<NodeId, string>();
    }

    public HashableOptimizedObservedRemoveSet<NodeId, string> IndexNames { get; }
    public HashableCrdtRegistry<NodeId, ShardId, ShardSizes> Shards { get; }

    public string Name
    {
        get => _name.Value;
        set => _name.Value = value;
    }

    public ulong Size => Shards.Values.Aggregate((ulong) 0, (a, b) => a + b.Size);
    public ulong StorageSize => Shards.Values.Aggregate((ulong) 0, (a, b) => a + b.StorageSize);

    /// <inheritdoc />
    public MergeResult Merge(CollectionInfoDto other) => _name.MaybeMerge(other.Name) ==
                                                         MergeResult.ConflictSolved // use | instead of || to prevent short circuit
                                                         | Shards.MaybeMerge(other.Shards) == MergeResult.ConflictSolved
                                                         | IndexNames.MaybeMerge(other.IndexNames) == MergeResult.ConflictSolved
                                                             ? MergeResult.ConflictSolved
                                                             : MergeResult.NotUpdated;

    /// <inheritdoc />
    public CollectionInfoDto ToDto() => new()
    {
        Name = _name.ToDto(),
        Shards = Shards.ToDto(),
        IndexNames = IndexNames.ToDto()
    };

    /// <inheritdoc />
    public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_name, Shards, IndexNames);

    [ProtoContract]
    public sealed class CollectionInfoDto
    {
        [ProtoMember(1)]
        public SingleValue<string>.SingleValueDto? Name { get; set; }

        [ProtoMember(2)]
        public HashableCrdtRegistry<NodeId, ShardId, ShardSizes>.HashableCrdtRegistryDto? Shards { get; set; }

        [ProtoMember(3)]
        public HashableOptimizedObservedRemoveSet<NodeId, string>.OptimizedObservedRemoveSetDto? IndexNames { get; set; }
    }

    public sealed class CollectionInfoFactory : ICRDTFactory<CollectionInfo, CollectionInfoDto>
    {
        /// <inheritdoc />
        public CollectionInfo Create(CollectionInfoDto dto) => new(dto);
    }
}

[ProtoContract]
public struct ShardSizes : IHashable, IEquatable<ShardSizes>
{
    [ProtoMember(1)]
    public ulong StorageSize;

    [ProtoMember(2)]
    public ulong Size;

    public ShardSizes(ulong size, ulong storageSize)
    {
        Size = size;
        StorageSize = storageSize;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> CalculateHash()
    {
        Span<byte> result = new byte[2 * sizeof(ulong)];
        BitConverter.TryWriteBytes(result[..sizeof(ulong)], StorageSize);
        BitConverter.TryWriteBytes(result[sizeof(ulong)..], Size);
        return result;
    }

    /// <inheritdoc />
    public bool Equals(ShardSizes other) => StorageSize == other.StorageSize && Size == other.Size;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ShardSizes other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StorageSize, Size);

    public static bool operator ==(ShardSizes left, ShardSizes right) => left.Equals(right);
    public static bool operator !=(ShardSizes left, ShardSizes right) => !left.Equals(right);
}
