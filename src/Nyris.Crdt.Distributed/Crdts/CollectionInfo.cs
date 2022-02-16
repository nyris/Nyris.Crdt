using System;
using System.Collections;
using System.Collections.Generic;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class CollectionInfo : ICRDT<CollectionInfo.CollectionInfoDto>, IHashable
    {
        private readonly SingleValue<string> _name;
        private readonly SingleValue<ulong> _size;

        public CollectionInfo(string name = "", ulong size = 0, IEnumerable<string>? indexes = null)
        {
            _name = new SingleValue<string>(name);
            _size = new SingleValue<ulong>(size);
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
            _size = ReferenceEquals(dto.Size, null) ? new SingleValue<ulong>(0) : new SingleValue<ulong>(dto.Size);
            IndexNames = dto.IndexNames != null
                ? HashableOptimizedObservedRemoveSet<NodeId, string>.FromDto(dto.IndexNames)
                : new HashableOptimizedObservedRemoveSet<NodeId, string>();
        }

        public HashableOptimizedObservedRemoveSet<NodeId, string> IndexNames { get; }

        public string Name
        {
            get => _name.Value;
            set => _name.Value = value;
        }

        public ulong Size
        {
            get => _size.Value;
            set => _size.Value = value;
        }

        /// <inheritdoc />
        public MergeResult Merge(CollectionInfoDto other)
        {
            return _name.MaybeMerge(other.Name) == MergeResult.ConflictSolved // use | instead of || to prevent short circuit
                   | _size.MaybeMerge(other.Size) == MergeResult.ConflictSolved
                ? MergeResult.ConflictSolved
                : MergeResult.NotUpdated;
        }

        /// <inheritdoc />
        public CollectionInfoDto ToDto() => new()
        {
            Name = _name.ToDto(),
            Size = _size.ToDto(),
            IndexNames = IndexNames.ToDto()
        };

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_name, _size);

        [ProtoContract]
        public sealed class CollectionInfoDto
        {
            [ProtoMember(1)]
            public SingleValue<string>.SingleValueDto? Name { get; set; }

            [ProtoMember(2)]
            public SingleValue<ulong>.SingleValueDto? Size { get; set; }

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