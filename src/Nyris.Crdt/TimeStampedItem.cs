using System;

namespace Nyris.Crdt
{
    public struct TimeStampedItem<TValue, TTimeStamp>
        where TTimeStamp : IComparable<TTimeStamp>
    {
        public TValue Value;

        public TTimeStamp TimeStamp;

        public bool Deleted;

        public TimeStampedItem(TValue value, TTimeStamp timeStamp, bool deleted)
        {
            Value = value;
            TimeStamp = timeStamp;
            Deleted = deleted;
        }
    }
}