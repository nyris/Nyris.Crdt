using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;
using System;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class SingleValue<T> : ICRDT<SingleValue<T>.SingleValueDto>, IHashable
        where T : IEquatable<T>
    {
        private DateTime _dateTime;
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (_value.Equals(value)) return;
                _value = value;
                _dateTime = DateTime.UtcNow;
            }
        }

        public SingleValue(T value)
        {
            _value = value;
            _dateTime = DateTime.UtcNow;
        }

        internal SingleValue(SingleValueDto dto)
        {
            _value = dto.Value!;
            _dateTime = dto.DateTime;
        }

        /// <inheritdoc />
        public MergeResult Merge(SingleValueDto other)
        {
            if (other.DateTime > _dateTime)
            {
                _value = other.Value!;
                _dateTime = other.DateTime;
                return MergeResult.ConflictSolved;
            }

            return other.DateTime == _dateTime ? MergeResult.Identical : MergeResult.NotUpdated;
        }

        /// <inheritdoc />
        public SingleValueDto ToDto() => new() { Value = _value, DateTime = _dateTime };

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => HashingHelper.CalculateHash(_value); // datetime is not important

        [ProtoContract]
        public sealed class SingleValueDto
        {
            [ProtoMember(1)]
            public T? Value { get; set; }

            [ProtoMember(2)]
            public DateTime DateTime { get; set; }
        }

        public sealed class SingleValueFactory : ICRDTFactory<SingleValue<T>, SingleValueDto>
        {
            /// <inheritdoc />
            public SingleValue<T> Create(SingleValueDto dto) => new(dto);
        }
    }
}
