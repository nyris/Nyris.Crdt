using System.Collections.Immutable;
using Nyris.Crdt;
using Nyris.Crdt.Interfaces;

namespace Nyris.ManagedCrdtsV2;

public sealed class CrdtConfig : IDeltaCrdt<CrdtConfig.DeltaDto, CrdtConfig.CausalTimestamp>
{
    private readonly Dictionary<ConfigFields, TimestampedValue<uint>> _uintValues = new()
    {
        [ConfigFields.RequestedReplicasCount] = 0,
        [ConfigFields.RequiresSyncsBeforeReplicaIsValid] = 1,
        [ConfigFields.NumberOfShards] = 1,
    };

    private readonly Dictionary<ConfigFields, TimestampedValue<string>> _stringValues = new()
    {
        [ConfigFields.PropagationStrategy] = "",
        [ConfigFields.ReroutingStrategy] = "",
        [ConfigFields.DistributionStrategy] = ""
    };

    public string FullTypeName
    {
        get => _stringValues.GetValueOrDefault(ConfigFields.FullTypeName);
        set => _stringValues[ConfigFields.FullTypeName] = value;
    }

    public uint RequestedReplicasCount
    {
        get => _uintValues.GetValueOrDefault(ConfigFields.RequestedReplicasCount);
        set => _uintValues[ConfigFields.RequestedReplicasCount] = value;
    }

    public abstract record DeltaDto(DateTime DateTime);
    public sealed record UintDto(ConfigFields Field, uint Value, DateTime DateTime) : DeltaDto(DateTime);
    public sealed record StringDto(ConfigFields Field, string Value, DateTime DateTime) : DeltaDto(DateTime);
    

    public sealed record CausalTimestamp(ImmutableDictionary<ConfigFields, DateTime> DateTimes);

    public CausalTimestamp GetLastKnownTimestamp()
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

        return new CausalTimestamp(builder.ToImmutable());
    }

    public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? timestamp = default)
    {
        var since = timestamp?.DateTimes ?? ImmutableDictionary<ConfigFields, DateTime>.Empty;
        foreach (var (field, value) in _uintValues)
        {
            if (!since.TryGetValue(field, out var sinceDateTime) || value.DateTime > sinceDateTime)
            {
                yield return new UintDto(field, value.Value, value.DateTime);
            }
        }
        foreach (var (field, value) in _stringValues)
        {
            if (!since.TryGetValue(field, out var sinceDateTime) || value.DateTime > sinceDateTime)
            {
                yield return new StringDto(field, value.Value, value.DateTime);
            }
        }
    }

    public DeltaMergeResult Merge(DeltaDto delta)
    {
        switch (delta)
        {
            case StringDto stringDto:
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
            case UintDto uintDto:
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
    
    public enum ConfigFields
    {
        RequestedReplicasCount = 1,
        RequiresSyncsBeforeReplicaIsValid = 2,
        NumberOfShards = 3,
        PropagationStrategy = 4,
        ReroutingStrategy = 5,
        DistributionStrategy = 6,
        FullTypeName = 7
    }
}