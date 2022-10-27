using System.Collections.Immutable;
using Newtonsoft.Json;
using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Sets;

namespace Nyris.Crdt.AspNetExampleV2;


// ReSharper disable once ClassNeverInstantiated.Global
public class ManagedSet 
    : ManagedCrdt<
        OptimizedObservedRemoveSetV3<NodeId, double>,
        OptimizedObservedRemoveCore<NodeId, double>.DeltaDto, 
        OptimizedObservedRemoveCore<NodeId, double>.CausalTimestamp, 
        SetOperation,
        Bool>
{
    private readonly NodeId _thisNode;
    private ulong _counter;
    private readonly ILogger<ManagedSet> _logger;
    public ManagedSet(InstanceId instanceId,
        NodeId thisNode, 
        ISerializer serializer, 
        IPropagationService propagationService,
        IReroutingService reroutingService,
        ILogger<ManagedSet> logger) 
        : base(instanceId, serializer, propagationService, logger, reroutingService)
    {
        _thisNode = thisNode;
        _logger = logger;
    }
    
    public async Task AddAsync(double item, CancellationToken cancellationToken)
    {
        var shardId = GetShardId(item);
        var context = new OperationContext(_thisNode, -1, GetTraceId("add"), cancellationToken);
        await AddAsync(shardId, item, context);
    }
    
    public async Task<bool> RemoveAsync(double item, CancellationToken cancellationToken)
    {
        var shardId = GetShardId(item);
        var context = new OperationContext(_thisNode, -1, GetTraceId("del"), cancellationToken);
        return await RemoveAsync(shardId, item, context);
    }
    
    protected override async Task<Bool> ApplyAsync(ShardId shardId, SetOperation operation, OperationContext context)
    {
        switch (operation)
        {
            case Add add:
                await AddAsync(shardId, add.Value, context);
                return new Bool(true);
            case Contains contains:
                throw new NotImplementedException();
            case Remove remove:
                var success = await RemoveAsync(shardId, remove.Value, context);
                return new Bool(success);
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static ShardId GetShardId(double value) => ShardId.FromUint((uint)(value % 3));
    
    private async Task AddAsync(ShardId shardId, double item, OperationContext context)
    {
        ImmutableArray<OptimizedObservedRemoveCore<NodeId, double>.DeltaDto> deltas;
        await WriteLock.WaitAsync(context.CancellationToken);
        try
        {
            // for addition we can always accept write locally, it will be relocated later if necessary
            // this should be a conscious choice - if better consistency is desired, rerouting writes same as reads is better 
            var (isReadReplica, shard) = GetOrCreateShard(shardId);
            
            if (!isReadReplica)
            {
                _logger.LogInformation("Shard {ShardId} was not found locally, rerouting addition of {Item}",
                    shardId, item);
                var operation = new Add(item);
                try
                {
                    await RerouteAsync(shardId, operation, context);
                    return;
                }
                catch (RoutingException)
                {
                    deltas = shard.Add(item, _thisNode);
                    _logger.LogInformation("Adding {Item} locally because it could not be propagated, resulting deltas: {Deltas}",
                        item, JsonConvert.SerializeObject(deltas));
                }
            }
            else
            {
                deltas = shard.Add(item, _thisNode);
                _logger.LogInformation("Adding {Item} locally resulted in deltas: {Deltas}",
                    item, JsonConvert.SerializeObject(deltas));
            }
        }
        finally
        {
            WriteLock.Release();
        }
        await PropagateAsync(shardId, deltas, context);
    }

    private async Task<bool> RemoveAsync(ShardId shardId, double item, OperationContext context)
    {
        ImmutableArray<OptimizedObservedRemoveCore<NodeId, double>.DeltaDto> deltas;

        await WriteLock.WaitAsync(context.CancellationToken);
        try
        {
            var (isReadReplica, shard) = GetOrCreateShard(shardId);

            if (isReadReplica)
            {
                deltas = shard.Remove(item);
                _logger.LogInformation("Removing {Item} locally resulted in {DeltasCount} Deltas: {Deltas}", 
                    item, deltas.Length, JsonConvert.SerializeObject(deltas));
            }
            else
            {
                _logger.LogInformation("Shard {ShardId} was not found locally, rerouting deletion of {Item}", 
                    shardId, item);
                var operation = new Remove(item);
                var result = await RerouteAsync(shardId, operation, context);
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

    private string GetTraceId(string method) => $"{InstanceId}-{method}-#{Interlocked.Increment(ref _counter)}";
}