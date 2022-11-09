namespace Nyris.Crdt.Managed.Metadata;

internal readonly struct TimestampedValue<T>
{
    public readonly DateTime DateTime;
    public readonly T Value;

    public TimestampedValue(T value, DateTime dateTime)
    {
        Value = value;
        DateTime = dateTime;
    }

    public static implicit operator T(TimestampedValue<T> v) => v.Value;
    // public static implicit operator TimestampedValue<T>(T value) => new(value, DateTime.UtcNow);
}