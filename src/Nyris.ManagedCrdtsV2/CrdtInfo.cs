using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Nyris.Crdt;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Sets;

namespace Nyris.ManagedCrdtsV2;

public sealed class CrdtInfo : IDeltaCrdt<CrdtInfo.DeltaDto, CrdtInfo.CausalTimestamp>
{
    // (instanceId, shardId) -> { storageSize, [ nodesWithReplica ] }
    // (instanceId, shardId) assigned to a node
    // -> created locally, added to Holder
    // -> add to a list of "potential valid replicas"
    // -> sync service reports successes to ClusterManager 

    private TimestampedValue<ulong> _storageSize = new(0, DateTime.MinValue);
    private readonly OptimizedObservedRemoveSetV2<NodeId, NodeId> _nodesWithReplica = new();

    public ulong StorageSize => _storageSize.Value;

    public CausalTimestamp GetLastKnownTimestamp() 
        => new(_storageSize.DateTime, _nodesWithReplica.GetLastKnownTimestamp());

    public IEnumerable<DeltaDto> EnumerateDeltaDtos(CausalTimestamp? since = default)
    {
        if (since is null || since.StorageSize < _storageSize.DateTime)
        {
            yield return new StorageSizeDto(_storageSize.Value, _storageSize.DateTime);
        }

        foreach (var dto in _nodesWithReplica.EnumerateDeltaDtos(since?.Nodes).Chunk(100))
        {
            var localRef = dto;
            yield return new NodesWithReplicaDto(
                Unsafe.As<
                    OptimizedObservedRemoveSetV2<NodeId,NodeId>.DeltaDto[], 
                    ImmutableArray<OptimizedObservedRemoveSetV2<NodeId,NodeId>.DeltaDto>>(ref localRef));
        }
    }

    public DeltaMergeResult Merge(DeltaDto delta)
    {
        switch (delta)
        {
            case NodesWithReplicaDto nodesWithReplicaDto:
                return _nodesWithReplica.Merge(nodesWithReplicaDto.Delta);
            case StorageSizeDto storageSizeDto:
                if (_storageSize.DateTime >= storageSizeDto.DateTime) return DeltaMergeResult.StateNotChanged;
                
                _storageSize = new TimestampedValue<ulong>(storageSizeDto.Value, storageSizeDto.DateTime);
                return DeltaMergeResult.StateUpdated;
            default:
                throw new ArgumentOutOfRangeException(nameof(delta));
        }
    }


    public abstract record DeltaDto;
    public sealed record NodesWithReplicaDto(ImmutableArray<OptimizedObservedRemoveSetV2<NodeId, NodeId>.DeltaDto> Delta) : DeltaDto;
    public sealed record StorageSizeDto(ulong Value, DateTime DateTime) : DeltaDto;

    public sealed record CausalTimestamp(DateTime StorageSize,
        OptimizedObservedRemoveSetV2<NodeId, NodeId>.CausalTimestamp Nodes);
}