using MessagePack;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.AspNetExampleV2;

/// <summary>
/// You can define your own CRDTs and use them as values in the map or directly in ManagedCrdts
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class DatedValue<T> : IDeltaCrdt<DatedValue<T>.Delta, DateTime>
{
    private DateTime _dateTime;
    private T _value;

    public DatedValue(T value)
    {
        _value = value;
        _dateTime = DateTime.UtcNow;
    }

    public DatedValue()
    {
        _value = default!;
        _dateTime = DateTime.MinValue;
    }

    public T Value
    {
        get => _value;
        set
        {
            _dateTime = DateTime.UtcNow;
            _value = value;
        }
    }

    public DateTime GetLastKnownTimestamp() => _dateTime;

    public IEnumerable<Delta> EnumerateDeltaDtos(DateTime timestamp = default)
    {
        if (timestamp < _dateTime) yield return new Delta(Value, _dateTime);
    }

    public DeltaMergeResult Merge(Delta delta)
    {
        if (delta.DateTime > _dateTime)
        {
            Value = delta.Value;
            _dateTime = delta.DateTime;
            return DeltaMergeResult.StateUpdated;
        }

        return DeltaMergeResult.StateNotChanged;
    }

    [MessagePackObject] public sealed record Delta([property: Key(0)] T Value, [property: Key(1)] DateTime DateTime);
}