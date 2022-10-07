using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Exceptions;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.ManagedCrdtsV2;

internal sealed class ManagedCrdtFactory : IManagedCrdtFactory
{
    private static readonly Dictionary<Type, int> TypeMap = new()
    {
        [typeof(InstanceId)] = 1,
        [typeof(ISerializer)] = 2,
        [typeof(IPropagationService)] = 3,
        [typeof(NodeId)] = 4,
        [typeof(NodeInfo)] = 5
    };

    private readonly INodeSelectionStrategy _nodeSelectionStrategy;
    private readonly INodeClientPool _nodeClientPool;
    private readonly ISerializer _serializer;
    private readonly ILogger<ManagedCrdtFactory> _logger;
    private readonly NodeInfo _thisNode;
    private readonly ILoggerFactory _loggerFactory;

    public ManagedCrdtFactory(INodeSelectionStrategy nodeSelectionStrategy,
        INodeClientPool nodeClientPool,
        ISerializer serializer, 
        ILogger<ManagedCrdtFactory> logger,
        NodeInfo thisNode,
        ILoggerFactory loggerFactory)
    {
        _nodeSelectionStrategy = nodeSelectionStrategy;
        _nodeClientPool = nodeClientPool;
        _serializer = serializer;
        _logger = logger;
        _thisNode = thisNode;
        _loggerFactory = loggerFactory;
    }

    public ManagedCrdt Create(string typeName, InstanceId instanceId, IReplicaDistributor replicaDistributor)
    {
        _logger.LogDebug("Creating ManagedCrdt based on type name {TypeName}", typeName);
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
                3 => new PropagationService(distributor, _nodeSelectionStrategy, _nodeClientPool),
                4 => _thisNode.Id,
                5 => _thisNode,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        return Activator.CreateInstance(type, args) ?? throw new InvalidOperationException();
    }
}