using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.Consistency;
using Nyris.Crdt.Distributed.Utils;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Services;

internal sealed class ConsistencyService<TCrdt, TDto> : BackgroundService
    where TCrdt : ManagedCRDT<TDto>
{
    private readonly ManagedCrdtContext _context;
    private readonly IChannelManager _channelManager;
    private readonly IConsistencyCheckTargetsSelectionStrategy _strategy;
    private readonly NodeId _thisNodeId;
    private readonly TimeSpan _delayBetweenChecks = TimeSpan.FromSeconds(20);

    private readonly ILogger<ConsistencyService<TCrdt, TDto>> _logger;

    /// <inheritdoc />
    public ConsistencyService(
        ManagedCrdtContext context,
        IChannelManager channelManager,
        IConsistencyCheckTargetsSelectionStrategy strategy,
        NodeInfo thisNode,
        ILogger<ConsistencyService<TCrdt, TDto>> logger
    )
    {
        _context = context;
        _channelManager = channelManager;
        _strategy = strategy;
        _logger = logger;
        _thisNodeId = thisNode.Id;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Executing for {CrdtType}", typeof(TCrdt));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryHandleConsistencyCheckAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception for Crdt of type {CrdtType}", typeof(TCrdt));
            }

            await Task.Delay(_delayBetweenChecks, stoppingToken);
        }
    }

    private async Task TryHandleConsistencyCheckAsync(CancellationToken cancellationToken)
    {
        var typeName = TypeNameCompressor.GetName<TCrdt>();

        foreach (var instanceId in _context.GetInstanceIds<TCrdt>())
        {
            var nameAndInstanceId = new TypeNameAndInstanceId(typeName, instanceId);
            var nodesThatShouldHaveReplica = _context.GetNodesThatHaveReplica(nameAndInstanceId).ToList();

            // _logger.LogDebug("Crdt {CrdtName} ({InstanceId}) is expected to be at {NodeList}",
            //     typeName, instanceId, string.Join(";", nodesThatShouldHaveReplica.Select(ni => ni.Id)));

            // if this instance was removed globally (i.e. no nodes should have a replica)
            // or if this particular node should not have a replica
            // then local replica may be deleted 
            var markForDeletion = nodesThatShouldHaveReplica.Count == 0
                                  || nodesThatShouldHaveReplica.All(ni => ni.Id != _thisNodeId);

            // however, local replica should be deleted only if it transferred all it's content
            // to nodes that should have it. That is - if synchronization was successful
            foreach (var nodeId in _strategy.GetTargetNodes(nodesThatShouldHaveReplica, _thisNodeId))
            {
                markForDeletion &= await TryHandleConsistencyCheckAsync(nameAndInstanceId, nodeId, cancellationToken);
            }

            if (markForDeletion)
            {
                _logger.LogDebug("Crdt {CrdtName} ({InstanceId}) will be removed locally",
                                 typeName, instanceId);
                await _context.RemoveLocallyAsync<TCrdt, TDto>(nameAndInstanceId, cancellationToken);
            }
        }
    }

    private async Task<bool> TryHandleConsistencyCheckAsync(
        TypeNameAndInstanceId nameAndInstanceId,
        NodeId nodeId,
        CancellationToken cancellationToken
    )
    {
        if (!_channelManager.TryGet<IDtoPassingGrpcService<TDto>>(nodeId, out var client))
        {
            _logger.LogError("Could not get a proxy to node with Id {NodeId}", nodeId);
            return false;
        }

        var hash = await client.GetHashAsync(nameAndInstanceId, cancellationToken);
        if (_context.IsHashEqual(nameAndInstanceId, hash))
        {
            // _logger.LogDebug("Crdt {CrdtType} with id {CrdtInstanceId} is consistent between " +
            // "this node ({ThisNodeId}) and node with id {NodeId}. Hash: '{MyHash}'",
            // nameAndInstanceId.TypeName,
            // nameAndInstanceId.InstanceId,
            // _thisNodeId,
            // nodeId,
            // Convert.ToHexString(hash));
            return true;
        }

        _logger.LogInformation("Crdt {CrdtType} with id {CrdtInstanceId} is not consistent between " +
                               "this node ({ThisNodeId}, hash: '{MyHash}') " +
                               "and node {NodeId} (hash: '{ReceivedHash}')",
                               nameAndInstanceId.TypeName,
                               nameAndInstanceId.InstanceId,
                               _thisNodeId,
                               Convert.ToHexString(_context.GetHash(nameAndInstanceId)),
                               nodeId,
                               Convert.ToHexString(hash));
        return await SyncCrdtsAsync(client, nameAndInstanceId.TypeName, nameAndInstanceId.InstanceId, cancellationToken);
    }

    private async Task<bool> SyncCrdtsAsync(
        IDtoPassingGrpcService<TDto> dtoPassingGrpcService,
        string typeName,
        InstanceId instanceId,
        CancellationToken cancellationToken
    )
    {
        // _logger.LogDebug("Syncing CRDT of type {CrdtType} with instanceId {InstanceId}", typeof(TCrdt), instanceId);

        var enumerable = _context.EnumerateDtoBatchesAsync<TCrdt, TDto>(instanceId, cancellationToken);
        var callOptions = new CallOptions(new Metadata
        {
            new("instance-id", instanceId.ToString()),
            new("crdt-type-name", typeName)
        }, cancellationToken: cancellationToken);
        await foreach (var dto in dtoPassingGrpcService.EnumerateCrdtAsync(enumerable, callOptions).WithCancellation(cancellationToken))
        {
            await _context.MergeAsync<TCrdt, TDto>(dto, instanceId, allowPropagation: false, cancellationToken: cancellationToken);
        }

        return true;
    }
}
