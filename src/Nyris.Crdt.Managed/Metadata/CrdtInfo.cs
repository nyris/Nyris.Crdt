using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.Managed.Metadata;

internal sealed class CrdtInfo : IDeltaCrdt<CrdtInfoDelta, CrdtInfoCausalTimestamp>
{
    private TimestampedValue<ulong> _storageSize = new(0, DateTime.MinValue);
    private readonly OptimizedObservedRemoveSetV2<NodeId, NodeId> _nodesWithReadReplica = new();

    public ulong StorageSize => _storageSize.Value;

    public HashSet<NodeId> ReadReplicas => _nodesWithReadReplica.Values;

    public ImmutableArray<CrdtInfoDelta> AddNode(NodeId nodeId)
    {
        var deltas = _nodesWithReadReplica.Add(nodeId, nodeId);
        return deltas.IsEmpty 
            ? ImmutableArray<CrdtInfoDelta>.Empty 
            : ImmutableArray.Create<CrdtInfoDelta>(new CrdtInfoNodesWithReplicaDelta(deltas));
    }

    public ImmutableArray<CrdtInfoDelta> RemoveNode(NodeId nodeId)
    {
        var deltas = _nodesWithReadReplica.Remove(nodeId);
        return deltas.IsEmpty 
            ? ImmutableArray<CrdtInfoDelta>.Empty 
            : ImmutableArray.Create<CrdtInfoDelta>(new CrdtInfoNodesWithReplicaDelta(deltas));
    }

    public bool DoesNodeHaveReadReplica(NodeId nodeId) => _nodesWithReadReplica.Contains(nodeId);

    public CrdtInfoCausalTimestamp GetLastKnownTimestamp() 
        => new(_storageSize.DateTime, _nodesWithReadReplica.GetLastKnownTimestamp());

    public IEnumerable<CrdtInfoDelta> EnumerateDeltaDtos(CrdtInfoCausalTimestamp? since = default)
    {
        if (since is null || since.StorageSize < _storageSize.DateTime)
        {
            yield return new CrdtInfoStorageSizeDelta(_storageSize.Value, _storageSize.DateTime);
        }

        foreach (var dto in _nodesWithReadReplica.EnumerateDeltaDtos(since?.Nodes).Chunk(100))
        {
            var localRef = dto;
            yield return new CrdtInfoNodesWithReplicaDelta(
                Unsafe.As<
                    OptimizedObservedRemoveSetV2<NodeId,NodeId>.DeltaDto[], 
                    ImmutableArray<OptimizedObservedRemoveSetV2<NodeId,NodeId>.DeltaDto>>(ref localRef));
        }
    }

    public DeltaMergeResult Merge(CrdtInfoDelta delta)
    {
        switch (delta)
        {
            case CrdtInfoNodesWithReplicaDelta nodesWithReplicaDto:
                return _nodesWithReadReplica.Merge(nodesWithReplicaDto.Delta);
            case CrdtInfoStorageSizeDelta storageSizeDto:
                if (_storageSize.DateTime >= storageSizeDto.DateTime) return DeltaMergeResult.StateNotChanged;
                
                _storageSize = new TimestampedValue<ulong>(storageSizeDto.Value, storageSizeDto.DateTime);
                return DeltaMergeResult.StateUpdated;
            default:
                throw new ArgumentOutOfRangeException(nameof(delta));
        }
    }
}