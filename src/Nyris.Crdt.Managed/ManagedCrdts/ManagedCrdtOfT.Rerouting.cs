using Microsoft.Extensions.Logging;
using Nyris.Crdt.Interfaces;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.Crdt.Managed.ManagedCrdts;

public abstract class ManagedCrdt<TCrdt, TDelta, TTimeStamp, TOperation, TOperationResult> : ManagedCrdt<TCrdt, TDelta, TTimeStamp>
    where TCrdt : IDeltaCrdt<TDelta, TTimeStamp>, new()
{
    private readonly ISerializer _serializer;
    private readonly IReroutingService _reroutingService;
    
    protected ManagedCrdt(InstanceId instanceId,
        ISerializer serializer,
        IPropagationService propagationService,
        ILogger logger,
        IReroutingService reroutingService) 
        : base(instanceId, serializer, propagationService, logger)
    {
        _serializer = serializer;
        _reroutingService = reroutingService;
    }

    public sealed override async Task<ReadOnlyMemory<byte>> ApplyAsync(ShardId shardId, ReadOnlyMemory<byte> operation,
        OperationContext context)
    {
        var result = await ApplyAsync(shardId, _serializer.Deserialize<TOperation>(operation), context);
        return _serializer.Serialize(result);
    }

    protected abstract Task<TOperationResult> ApplyAsync(ShardId shardId, TOperation operation, OperationContext context);

    protected async Task<TOperationResult> RerouteAsync(ShardId shardId, TOperation operation, OperationContext context)
    {
        var result = await _reroutingService.RerouteAsync(InstanceId, shardId, _serializer.Serialize(operation), context);
        return _serializer.Deserialize<TOperationResult>(result);
    }
}