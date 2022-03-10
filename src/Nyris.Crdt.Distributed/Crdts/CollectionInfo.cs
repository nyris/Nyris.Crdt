using System;
using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class CollectionInfo : ICRDT<CollectionInfo.CollectionInfoDto>, IHashable
    {
        private readonly SingleValue<string> _name;

        public CollectionInfo(string name = "",
            IEnumerable<ShardId>? shardIds = null,
            IEnumerable<string>? indexes = null)
        {
            _name = new SingleValue<string>(name);
            Shards = new();
            if (!ReferenceEquals(shardIds, null))
            {
                foreach (var shardId in shardIds)
                {
                    Shards.TryAdd(DefaultConfiguration.NodeInfoProvider.ThisNodeId, shardId, 0);
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

            Shards = new HashableCrdtRegistry<NodeId, ShardId, ulong>();
            Shards.MaybeMerge(dto.Shards);

            IndexNames = dto.IndexNames != null
                ? HashableOptimizedObservedRemoveSet<NodeId, string>.FromDto(dto.IndexNames)
                : new HashableOptimizedObservedRemoveSet<NodeId, string>();
        }

        public HashableOptimizedObservedRemoveSet<NodeId, string> IndexNames { get; }
        public HashableCrdtRegistry<NodeId, ShardId, ulong> Shards { get; }

        public string Name
        {
            get => _name.Value;
            set => _name.Value = value;
        }

        public ulong Size => Shards.Values.Aggregate((ulong)0, (a, b) => a + b);

        /// <inheritdoc />
        public MergeResult Merge(CollectionInfoDto other)
        {
            return _name.MaybeMerge(other.Name) == MergeResult.ConflictSolved // use | instead of || to prevent short circuit
                   | Shards.MaybeMerge(other.Shards) == MergeResult.ConflictSolved
                   | IndexNames.MaybeMerge(other.IndexNames) == MergeResult.ConflictSolved
                ? MergeResult.ConflictSolved
                : MergeResult.NotUpdated;
        }

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
            public HashableCrdtRegistry<NodeId, ShardId, ulong>.HashableCrdtRegistryDto? Shards { get; set; }

            [ProtoMember(3)]
            public HashableOptimizedObservedRemoveSet<NodeId, string>.OptimizedObservedRemoveSetDto? IndexNames { get; set; }
        }

        public sealed class CollectionInfoFactory : ICRDTFactory<CollectionInfo, CollectionInfoDto>
        {
            /// <inheritdoc />
            public CollectionInfo Create(CollectionInfoDto dto) => new(dto);
        }
    }
}