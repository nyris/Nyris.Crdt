using System;
using ProtoBuf;

namespace Nyris.Crdt.Model;

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

    // NOTE: Disabling in Global didn't work for some reason
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private TimeStampedItem()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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
