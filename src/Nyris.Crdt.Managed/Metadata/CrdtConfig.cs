using System.Collections.Immutable;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.Managed.Metadata;

internal sealed class CrdtConfig : IDeltaCrdt<CrdtConfigDelta, CrdtConfigCausalTimestamp>
{
    private readonly Dictionary<ConfigFields, TimestampedValue<uint>> _uintValues = new();
    private readonly Dictionary<ConfigFields, TimestampedValue<string>> _stringValues = new();

    public string FullTypeName
    {
        get => _stringValues.GetValueOrDefault(ConfigFields.FullTypeName);
        set => _stringValues[ConfigFields.FullTypeName] = new TimestampedValue<string>(value, DateTime.UtcNow);
    }

    public uint RequestedReplicasCount
    {
        get => _uintValues.GetValueOrDefault(ConfigFields.RequestedReplicasCount);
        set => _uintValues[ConfigFields.RequestedReplicasCount] = new TimestampedValue<uint>(value, DateTime.UtcNow);
    }
    
    public uint NumberOfShards
    {
        get => _uintValues.GetValueOrDefault(ConfigFields.NumberOfShards);
        set => _uintValues[ConfigFields.NumberOfShards] = new TimestampedValue<uint>(value, DateTime.UtcNow);
    }


    public CrdtConfigCausalTimestamp GetLastKnownTimestamp()
    {
        var builder = ImmutableDictionary.CreateBuilder<ConfigFields, DateTime>();
        foreach (var (field, value) in _uintValues)
        {
            builder.Add(field, value.DateTime);
        }

        foreach (var (field, value) in _stringValues)
        {
            builder.Add(field, value.DateTime);
        }

        return new CrdtConfigCausalTimestamp(builder.ToImmutable());
    }

    public IEnumerable<CrdtConfigDelta> EnumerateDeltaDtos(CrdtConfigCausalTimestamp? timestamp = default)
    {
        var since = timestamp?.DateTimes ?? ImmutableDictionary<ConfigFields, DateTime>.Empty;
        foreach (var (field, value) in _uintValues)
        {
            if (!since.TryGetValue(field, out var sinceDateTime) || value.DateTime > sinceDateTime)
            {
                yield return new CrdtConfigUintDelta(field, value.Value, value.DateTime);
            }
        }
        foreach (var (field, value) in _stringValues)
        {
            if (!since.TryGetValue(field, out var sinceDateTime) || value.DateTime > sinceDateTime)
            {
                yield return new CrdtConfigStringDelta(field, value.Value, value.DateTime);
            }
        }
    }

    public DeltaMergeResult Merge(CrdtConfigDelta delta)
    {
        switch (delta)
        {
            case CrdtConfigStringDelta stringDto:
                lock (_stringValues)
                {
                    if (!_stringValues.TryGetValue(stringDto.Field, out var value))
                    {
                        _stringValues[stringDto.Field] = new TimestampedValue<string>(stringDto.Value, stringDto.DateTime);
                        return DeltaMergeResult.StateUpdated;
                    }

                    if (value.DateTime >= delta.DateTime) return DeltaMergeResult.StateNotChanged;
                    _stringValues[stringDto.Field] = new TimestampedValue<string>(stringDto.Value, stringDto.DateTime);
                    return DeltaMergeResult.StateUpdated;
                }
            case CrdtConfigUintDelta uintDto:
                lock (_uintValues)
                {
                    if (!_uintValues.TryGetValue(uintDto.Field, out var value))
                    {
                        _uintValues[uintDto.Field] = new TimestampedValue<uint>(uintDto.Value, uintDto.DateTime);
                        return DeltaMergeResult.StateUpdated;
                    }

                    if (value.DateTime >= delta.DateTime) return DeltaMergeResult.StateNotChanged;
                    _uintValues[uintDto.Field] = new TimestampedValue<uint>(uintDto.Value, uintDto.DateTime);
                    return DeltaMergeResult.StateUpdated;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(delta));
        }
    }
}