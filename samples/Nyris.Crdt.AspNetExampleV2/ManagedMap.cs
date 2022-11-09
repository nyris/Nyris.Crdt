using System.Collections.Concurrent;
using System.Collections.Immutable;
using Newtonsoft.Json;
using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExampleV2;

public sealed class ManagedMap 
    : ManagedCrdt<
        ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime>,
        ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime>.DeltaDto,
        ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime>.CausalTimestamp,
        MapOperation,
        MapOperationResult
    >
{
    private ConcurrentDictionary<ShardId, BruteForceIndex> _indexes = new();
    private readonly NodeId _thisNode;
    private readonly ILogger<ManagedMap> _logger;
    private static readonly Empty EmptyResult = new();

    // hardcode 3 shards for demo purposes 
    private readonly ShardId[] _allShards = { ShardId.FromUint(0), ShardId.FromUint(1), ShardId.FromUint(2) };
    
    public ManagedMap(InstanceId instanceId,
        NodeId thisNode,
        ISerializer serializer,
        IPropagationService propagationService,
        IReroutingService reroutingService,
        ILogger<ManagedMap> logger) 
        : base(instanceId, serializer, propagationService, reroutingService)
    {
        _thisNode = thisNode;
        _logger = logger;
    }

    public async Task AddAsync(ImageId id, float[] vector, CancellationToken cancellationToken)
    {
        var shardId = GetShardId(id);
        var context = new OperationContext(_thisNode, 1, "add", cancellationToken);
        await AddAsync(shardId, id, vector, context);
    }
    
    public async Task<bool> RemoveAsync(ImageId id, CancellationToken cancellationToken)
    {
        var shardId = GetShardId(id);
        var context = new OperationContext(_thisNode, 1, "del", cancellationToken);
        return await RemoveAsync(shardId, id, context);
    }

    public async Task<(ImageId Id, float DotProduct)> FindClosest(float[] vector, CancellationToken cancellationToken)
    {
        var dotProducts = new (ImageId Id, float DotProduct)[_allShards.Length];
        var context = new OperationContext(_thisNode, 0, "find", cancellationToken);
        for (var i = 0; i < _allShards.Length; i++)
        {
            var shardId = _allShards[i];
            dotProducts[i] = await FindClosest(shardId, vector, context);
        }

        var result = dotProducts.MaxBy(tuple => tuple.DotProduct);
        
        _logger.LogInformation("Search aggregation found imageId {ImageId}, dotProduct: {DotProduct}", 
            result.Id, result.DotProduct);
        return result;
    }

    private async Task<(ImageId, float)> FindClosest(ShardId shardId, float[] vector, OperationContext context)
    {
        if (TryGetShard(shardId, out _, out var isReadReplica)
            && isReadReplica
            && _indexes.TryGetValue(shardId, out var index))
        {
            var foundId = index.Find(vector, out var dotProduct);
            _logger.LogInformation("Local search found imageId {ImageId}, dotProduct: {DotProduct}", 
                foundId, dotProduct);
            return (foundId, dotProduct);
        }

        var operation = new FindOperation(vector);
        var result = await RerouteAsync<SearchResult>(shardId, operation, context);
        return (result.Id, result.DotProduct);
    }
    
    private static ShardId GetShardId(ImageId id) => ShardId.FromUint((uint)(Math.Abs(id.GetHashCode()) % 3));

    private async Task AddAsync(ShardId shardId, ImageId id, float[] vector, OperationContext context)
    {
        ImmutableArray<ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime>.DeltaDto> deltas;
        await WriteLock.WaitAsync(context.CancellationToken);
        try
        { 
            var (_, shard) = GetOrCreateShard(shardId);
            deltas = shard.AddOrMerge(_thisNode, id, new DatedValue<float[]>(vector));
        }
        finally
        {
            WriteLock.Release();
        }
        
        if(!deltas.IsDefaultOrEmpty) await PropagateAsync(shardId, deltas, context);
    }

    private async Task<bool> RemoveAsync(ShardId shardId, ImageId id, OperationContext context)
    {
        ImmutableArray<ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime>.DeltaDto> deltas;

        await WriteLock.WaitAsync(context.CancellationToken);
        try
        {
            var (isReadReplica, shard) = GetOrCreateShard(shardId);

            if (isReadReplica)
            {
                shard.TryRemove(id, out deltas);
            }
            else
            {
                var operation = new RemoveOperation(id);
                var result = await RerouteAsync<RemovalResult>(shardId, operation, context);
                return result.Success;
            }
        }
        finally
        {
            WriteLock.Release();
        }

        if (!deltas.IsDefaultOrEmpty)
        {
            await PropagateAsync(shardId, deltas, context);
            return true;
        }

        return false;
    }

    protected override async Task<MapOperationResult> ApplyAsync(ShardId shardId, MapOperation operation, OperationContext context)
    {
        switch (operation)
        {
            case AddOperation addOperation:
                await AddAsync(shardId, addOperation.Id, addOperation.Vector, context);
                return EmptyResult;
            case FindOperation findOperation:
                var result = await FindClosest(shardId, findOperation.Vector, context);
                return new SearchResult(result.Item1, result.Item2);
            case RemoveOperation removeOperation:
                var success = await RemoveAsync(shardId, removeOperation.Id, context);
                return new RemovalResult(success);
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
    
    protected override Task DropShardAsync(in ShardId shardId)
    {
        _indexes.TryRemove(shardId, out _);
        return base.DropShardAsync(in shardId);
    }

    protected override ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime> CreateShard(in ShardId shardId)
    {
        var shard = new ObservedRemoveMapV2<NodeId, ImageId, DatedValue<float[]>, DatedValue<float[]>.Delta, DateTime>();
        var index = new BruteForceIndex();
        
        shard.SubscribeToChanges(index);
        _indexes[shardId] = index;
        
        return shard;
    }
}