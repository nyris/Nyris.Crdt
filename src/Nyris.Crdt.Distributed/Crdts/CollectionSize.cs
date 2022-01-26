using System;
using System.Threading;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class CollectionSize : ICRDT<CollectionSize, ulong, CollectionSize.CollectionSizeDto>, IHashable
    {
        private ulong _positive = 0;
        private ulong _negative = 0;

        /// <inheritdoc />
        public ulong Value => _positive - _negative;

        public CollectionSize()
        {
        }

        private CollectionSize(CollectionSizeDto dto)
        {
            _negative = dto.Negative;
            _positive = dto.Positive;
        }

        public void Increment() => Interlocked.Increment(ref _positive);
        public void Decrement() => Interlocked.Increment(ref _negative);
        public void Add(ulong value) => Interlocked.Add(ref _positive, value);
        public void Subtract(ulong value) => Interlocked.Add(ref _negative, value);

        /// <inheritdoc />
        public MergeResult Merge(CollectionSize other)
        {
            var conflict = other._positive != _positive || other._negative != _negative;
            if (!conflict) return MergeResult.Identical;

            _positive = Math.Max(other._positive, _positive);
            _negative = Math.Max(other._negative, _positive);
            return MergeResult.ConflictSolved;
        }

        /// <inheritdoc />
        public CollectionSizeDto ToDto() => new CollectionSizeDto { Positive = _positive, Negative = _negative };

        [ProtoContract]
        public sealed class CollectionSizeDto
        {
            [ProtoMember(1)]
            public ulong Positive { get; set; }

            [ProtoMember(2)]
            public ulong Negative { get; set; }
        }

        public sealed class CollectionSizeFactory : ICRDTFactory<CollectionSize, ulong, CollectionSizeDto>
        {
            /// <inheritdoc />
            public CollectionSize Create(CollectionSizeDto dto) => new(dto);
        }

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash()
        {
            var r = new Span<byte>(new byte[sizeof(ulong) * 2]);
            BitConverter.TryWriteBytes(r[..sizeof(ulong)], _positive);
            BitConverter.TryWriteBytes(r[sizeof(ulong)..], _negative);
            return r;
        }
    }
}