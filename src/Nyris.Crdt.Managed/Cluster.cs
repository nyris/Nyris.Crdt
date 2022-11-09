using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.Metadata;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed;

internal sealed partial class Cluster : ICluster, IManagedCrdtProvider
{
    public bool TryGet<TCrdt>(InstanceId instanceId, [NotNullWhen(true)] out TCrdt? crdt) 
        where TCrdt : ManagedCrdt
    {
        var result = _crdts.TryGetValue(instanceId, out var managedCrdt);
        crdt = managedCrdt as TCrdt;
        return result && crdt is not null;
    }

    public bool TryGet(InstanceId instanceId, [NotNullWhen(true)] out IManagedCrdt? crdt)
    {
        var result = _crdts.TryGetValue(instanceId, out var value);
        crdt = value;
        return result;
    }

    public async Task<TCrdt> CreateAsync<TCrdt>(InstanceId instanceId, CancellationToken cancellationToken)
        where TCrdt : ManagedCrdt
    {
        var traceId = $"{instanceId.ToString()}-creation";
        var context = new OperationContext(_thisNode.Id, -1, traceId, cancellationToken);
        var crdt = _crdtFactory.Create<TCrdt>(instanceId, this);
        _crdts.TryAdd(instanceId, crdt);

        // TODO: allow user to specify number of shards and replication factor
        var replicaIds = new[]
        { 
            instanceId.With(ShardId.FromUint(0)),
            instanceId.With(ShardId.FromUint(1)), 
            instanceId.With(ShardId.FromUint(2))
        };

        // TODO: it seems practical, but this requires more thinking.
        // This is done to avoid period between crdt creation and next sync when there are no read replicas anywhere
        // During this time it makes sense to allow this node to accept all operations. But there may be a better solution
        foreach (var replicaId in replicaIds)
        {
            crdt.MarkLocalShardAsReadReplica(replicaId.ShardId);
        }
        
        await Task.WhenAll(
            AddConfigAndPropagateAsync<TCrdt>(instanceId, context),
            AddInfosAndPropagateAsync(replicaIds, context));
        await DistributeShardsAsync(cancellationToken); // intentionally awaited after previous two

        return crdt;
    }
    
    private async Task AddConfigAndPropagateAsync<TCrdt>(InstanceId instanceId, OperationContext context)
    {
        ImmutableArray<CrdtConfigs.DeltaDto> deltas;
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            deltas = _crdtConfigs.AddOrMerge(_thisNode.Id, instanceId, new CrdtConfig
            {
                AssemblyQualifiedName = typeof(TCrdt).AssemblyQualifiedName ?? throw new AssumptionsViolatedException($"Type {typeof(TCrdt)} expected to have a full name"),
                RequestedReplicasCount = 2,
                NumberOfShards = 3  // TODO: allow user to set these
            });
        }
        finally
        {
            _semaphore.Release(); 
        }
        var deltasBin = _serializer.Serialize(deltas);
        var nodes = _nodeSet.Values.ToImmutableArray();
        await _propagationService.PropagateAsync(MetadataKind.CrdtConfigs, deltasBin, nodes, context);
    }

    private async Task AddInfosAndPropagateAsync(ReplicaId[] shardIds, OperationContext context)
    {
        var infosDeltas = new ImmutableArray<CrdtInfos.DeltaDto>[shardIds.Length];
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            for (var i = 0; i < shardIds.Length; i++)
            {
                var info = new CrdtInfo();
                info.AddNodeAsHoldingReadReplica(_thisNode.Id, _thisNode.Id);
                infosDeltas[i] = _crdtInfos.AddOrMerge(_thisNode.Id, shardIds[i], info);
            }
        }
        finally
        {
            _semaphore.Release();
        }
        await PropagateInfosAsync(infosDeltas.SelectMany(deltas => deltas), context);
    }
}