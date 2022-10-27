using Microsoft.Extensions.Logging;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Managed.Services;
using Nyris.Crdt.Managed.Services.Propagation;
using Nyris.Crdt.Managed.Strategies.NodeSelection;
using Nyris.Crdt.Serialization.Abstractions;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Managed.ManagedCrdts.Factory;

internal sealed class ManagedCrdtFactory : IManagedCrdtFactory
{
    private static readonly Dictionary<Type, int> TypeMap = new()
    {
        [typeof(InstanceId)] = 1,
        [typeof(ISerializer)] = 2,
        [typeof(IPropagationService)] = 3,
        [typeof(NodeId)] = 4,
        [typeof(NodeInfo)] = 5,
        [typeof(IReroutingService)] = 6
    };

    private readonly INodeSubsetSelectionStrategy _nodeSubsetSelectionStrategy;
    private readonly INodeSelectionStrategy _nodeSelectionStrategy;
    private readonly INodeClientFactory _nodeClientFactory;
    private readonly ISerializer _serializer;
    private readonly ILogger<ManagedCrdtFactory> _logger;
    private readonly NodeInfo _thisNode;
    private readonly ILoggerFactory _loggerFactory;

    public ManagedCrdtFactory(INodeSubsetSelectionStrategy nodeSubsetSelectionStrategy,
        INodeSelectionStrategy nodeSelectionStrategy,
        INodeClientFactory nodeClientFactory,
        ISerializer serializer, 
        ILogger<ManagedCrdtFactory> logger,
        NodeInfo thisNode,
        ILoggerFactory loggerFactory)
    {
        _nodeSubsetSelectionStrategy = nodeSubsetSelectionStrategy;
        _nodeSelectionStrategy = nodeSelectionStrategy;
        _nodeClientFactory = nodeClientFactory;
        _serializer = serializer;
        _logger = logger;
        _thisNode = thisNode;
        _loggerFactory = loggerFactory;
    }

    public ManagedCrdt Create(string typeName, InstanceId instanceId, IReplicaDistributor replicaDistributor)
    {
        var type = Type.GetType(typeName) ?? throw new AssumptionsViolatedException($"Could not find type {typeName}");
        return (ManagedCrdt)Create(type, instanceId, replicaDistributor);
    }
    
    public TCrdt Create<TCrdt>(InstanceId instanceId, IReplicaDistributor replicaDistributor) 
        => (TCrdt)Create(typeof(TCrdt), instanceId, replicaDistributor);

    private object Create(Type type, InstanceId instanceId, IReplicaDistributor distributor)
    {
        var constructorParams = type.GetConstructors()
            .OrderByDescending(info => info.GetParameters().Length)
            .First()
            .GetParameters();

        var args = new object[constructorParams.Length];

        for (var i = 0; i < constructorParams.Length; i++)
        {
            var paramInfo = constructorParams[i];

            if (paramInfo.ParameterType.IsAssignableTo(typeof(ILogger)))
            {
                if (paramInfo.ParameterType.IsGenericType)
                {
                    var loggerType = typeof(Logger<>).MakeGenericType(paramInfo.ParameterType.GetGenericArguments());
                    args[i] = Activator.CreateInstance(loggerType, _loggerFactory)!;
                }
                else
                {
                    args[i] = _loggerFactory.CreateLogger(type);
                }
                continue;
            }

            args[i] = TypeMap[paramInfo.ParameterType] switch
            {
                1 => instanceId,
                2 => _serializer,
                3 => new PropagationService(distributor, _nodeSubsetSelectionStrategy, _nodeClientFactory),
                4 => _thisNode.Id,
                5 => _thisNode,
                6 => new ReroutingService(distributor, _nodeClientFactory, _nodeSelectionStrategy, _thisNode),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        _logger.LogDebug("Running Activator with {Type} and constructor parameters: {Args}", 
            type, string.Join(", ", args));
        return Activator.CreateInstance(type, args) ?? throw new InvalidOperationException("Activator returned null");
    }
}