using System;
using ProtoBuf;

namespace Nyris.Crdt
{
    [ProtoContract]
    public sealed class TimeStampedItem<TValue, TTimeStamp>
        where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
    {
        [ProtoMember(1)]
        public TValue Value { get; set; }

        [ProtoMember(2)]
        public TTimeStamp TimeStamp { get; set; }

        [ProtoMember(3)]
        public bool Deleted { get; set; }

        private TimeStampedItem()
        {
            // parameterless constructor for Protobuf-NET
        }

        public TimeStampedItem(TValue value, TTimeStamp timeStamp, bool deleted)
        {
            Value = value;
            TimeStamp = timeStamp;
            Deleted = deleted;
        }
    }
}
