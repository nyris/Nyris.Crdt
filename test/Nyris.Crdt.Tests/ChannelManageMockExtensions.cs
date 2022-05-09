using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Tests;

internal static class ChannelManageMockExtensions
{
    public static Mock<IChannelManager> SetupOperationPassingForRegistry<TOperation, TResponse>(
        this Mock<IChannelManager> channelManagerMock,
        IList<NodeMock> nodes)
        where TOperation : RegistryOperation
        where TResponse : RegistryOperationResponse
        => channelManagerMock.SetupOperationPassing<PartiallyReplicatedImageInfoCollectionsRegistry,
            CollectionId,
            ImageInfoLwwCollectionWithSerializableOperations,
            ImageGuid,
            ImageInfo,
            ImageInfoLwwCollectionWithSerializableOperations.LastWriteWinsDto,
            RegistryOperation,
            RegistryOperationResponse,
            ImageInfoLwwCollectionWithSerializableOperations.
            ImageInfoLwwCollectionWithSerializableOperationsFactory,
            TOperation,
            TResponse>(nodes);

    public static Mock<IChannelManager> SetupOperationPassing<TCrdt,
        TKey,
        TCollection,
        TCollectionKey,
        TCollectionValue,
        TCollectionDto,
        TCollectionOperationBase,
        TCollectionOperationResponseBase,
        TCollectionFactory,
        TOperation,
        TResponse>(this Mock<IChannelManager> channelManagerMock,
        IList<NodeMock> nodes)
        where TCrdt : PartiallyReplicatedCRDTRegistry<TKey,
            TCollection,
            TCollectionKey,
            TCollectionValue,
            TCollectionDto,
            TCollectionOperationBase,
            TCollectionOperationResponseBase,
            TCollectionFactory>
        where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
        where TCollection : ManagedCrdtRegistryBase<TCollectionKey, TCollectionValue, TCollectionDto>,
        IAcceptOperations<TCollectionOperationBase, TCollectionOperationResponseBase>
        where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionDto>, new()
        where TCollectionOperationBase : Operation, ISelectShards
        where TCollectionOperationResponseBase : OperationResponse
        where TOperation : TCollectionOperationBase
        where TResponse : TCollectionOperationResponseBase
    {
        var clients = new List<IOperationPassingGrpcService<TOperation, TResponse>>(nodes.Count);
        clients.AddRange(nodes.Select(node => new TestOperationPassingGrpcService<TCrdt,
            TKey,
            TCollection,
            TCollectionKey,
            TCollectionValue,
            TCollectionDto,
            TCollectionOperationBase,
            TCollectionOperationResponseBase,
            TCollectionFactory,
            TOperation,
            TResponse>(node.Context)));

        for (var j = 0; j < nodes.Count; ++j)
        {
            var client = clients[j];
            var nodeId = nodes[j].Id;

            channelManagerMock.Setup(manager => manager.TryGet(nodeId, out client)).Returns(true);
        }

        return channelManagerMock;
    }
}
