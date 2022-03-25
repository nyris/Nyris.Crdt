using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Strategies.Propagation;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.Tests;

public class PropagationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly List<IHostedService> _hostedServices = new();

    public PropagationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task UpdateToImageInfoLwwCollectionPropagated(int nNodes)
    {
        var nodes = await NodeMock.PrepareNodeMocksAsync(nNodes);
        await StartPropagationServicesAsync<ImageInfoCollectionsRegistry, ImageInfoCollectionsRegistry.RegistryDto>(
            nodes);
        await StartPropagationServicesAsync<ImageInfoLwwCollection, ImageInfoLwwCollection.LastWriteWinsDto>(nodes);

        // create collection registry on all nodes
        var crdts = new List<ImageInfoCollectionsRegistry>(nNodes);
        foreach (var node in nodes)
        {
            var crdt = new ImageInfoCollectionsRegistry(new InstanceId("1"),
                queueProvider: node.QueueProvider,
                factory: new ImageInfoLwwCollection.ImageInfoLwwCollectionFactory(node.QueueProvider,
                    _output.BuildLogger()),
                logger: _output.BuildLogger());
            crdts.Add(crdt);
            node.Context.Add<ImageInfoCollectionsRegistry, ImageInfoCollectionsRegistry.RegistryDto>(crdt);
        }

        // add collection
        var collectionId = CollectionId.New();
        var collection = new ImageInfoLwwCollection(new InstanceId(collectionId.ToString()),
            queueProvider: nodes[0].QueueProvider,
            logger: _output.BuildLogger());
        await crdts[0].TryAddAsync(collectionId, nodes[0].Id, collection, nNodes - 1);

        // add item to collection
        var imageUuid = ImageGuid.New();
		var downloadUrl = new Uri("http://test.url");
		const string imageId = "1234";
        await collection.SetAsync(imageUuid, new ImageInfo(downloadUrl, imageId),
            DateTime.UtcNow, nNodes - 1);
        foreach (var crdt in crdts)
        {
            crdt.Size.Should().Be(1);
            crdt.TryGetValue(collectionId, out var extractedCollection).Should().BeTrue();
            extractedCollection!.Size.Should().Be(1);
            extractedCollection.Value.Should().ContainKey(imageUuid);
			extractedCollection.Value[imageUuid].ImageId.Should().Be(imageId);
			extractedCollection.Value[imageUuid].DownloadUrl.Should().Be(downloadUrl);
        }

        // remove item from collection
        await collection.RemoveAsync(imageUuid, DateTime.UtcNow, nNodes - 1);
        foreach (var crdt in crdts)
        {
            crdt.Size.Should().Be(1);
            crdt.TryGetValue(collectionId, out var extractedCollection).Should().BeTrue();
            extractedCollection!.Value.Should().NotContainKey(imageUuid);
        }
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(7, 1)]
	[InlineData(2, 3)]
	[InlineData(3, 3)]
	[InlineData(7, 3)]
	[InlineData(2, 15)]
	[InlineData(3, 15)]
	[InlineData(7, 15)]
    public async Task UpdateToPartiallyReplicatedImageInfoLwwCollectionPropagated(int nNodes, ushort nShards)
    {
        var nodes = await NodeMock.PrepareNodeMocksAsync(nNodes);
        await StartPropagationServicesAsync<PartiallyReplicatedImageInfoCollectionsRegistry,
			PartiallyReplicatedImageInfoCollectionsRegistry.PartiallyReplicatedCrdtRegistryDto>(nodes);
        await StartPropagationServicesAsync<ImageInfoLwwCollectionWithSerializableOperations,
			ImageInfoLwwCollectionWithSerializableOperations.LastWriteWinsDto>(nodes);

        // create collection registry on all nodes
        var crdts = new List<PartiallyReplicatedImageInfoCollectionsRegistry>(nNodes);
        foreach (var node in nodes)
		{
			var factory = new ImageInfoLwwCollectionWithSerializableOperations.
				ImageInfoLwwCollectionWithSerializableOperationsFactory(node.QueueProvider,
																		_output.BuildLogger());
			var channelManagerMock = new Mock<IChannelManager>()
				.SetupOperationPassingForRegistry<AddValueOperation<ImageGuid, ImageInfo, DateTime>,
					ValueResponse<ImageInfo>>(nodes)
				.SetupOperationPassingForRegistry<GetValueOperation<ImageGuid>, ValueResponse<ImageInfo>>(nodes);

            var crdt = new PartiallyReplicatedImageInfoCollectionsRegistry(new InstanceId("1"),
																		   logger: _output.BuildLogger(),
																		   nodeInfoProvider: node.InfoProvider,
																		   queueProvider: node.QueueProvider,
																		   channelManager: channelManagerMock.Object,
																		   factory: factory);
            crdts.Add(crdt);
            node.Context.Add<PartiallyReplicatedImageInfoCollectionsRegistry,
				PartiallyReplicatedImageInfoCollectionsRegistry.PartiallyReplicatedCrdtRegistryDto>(crdt);
        }

        // add collection
        var collectionId = CollectionId.New();
		await crdts[0].TryAddCollectionAsync(collectionId,
											 new CollectionConfig
											 {
												 Name = "test-collection",
												 ShardingConfig = new ShardingConfig { NumShards = nShards }
											 }, nNodes - 1);

        // add item to collection
        var imageUuid = ImageGuid.New();
		var downloadUrl = new Uri("http://test.url");
		const string imageId = "1234";
		var imageInfo = new ImageInfo(downloadUrl, imageId);
		var addValue = new AddValueOperation<ImageGuid, ImageInfo, DateTime>(imageUuid, imageInfo, DateTime.UtcNow);

		await crdts[0].ApplyAsync<AddValueOperation<ImageGuid, ImageInfo, DateTime>, ValueResponse<ImageInfo>>(collectionId,
																											   addValue,
																											   propagateToNodes: nNodes - 1);

        foreach (var crdt in crdts)
		{
			crdt.CollectionExists(collectionId).Should().BeTrue();
			crdt.TryGetCollectionSize(collectionId, out var size).Should().BeTrue();
			size.Should().Be(1);
			var response = await crdt.ApplyAsync<GetValueOperation<ImageGuid>,
				ValueResponse<ImageInfo>>(collectionId,
										  new GetValueOperation<ImageGuid>(imageUuid),
										  propagateToNodes: nNodes - 1);
			response.Value.Should().NotBeNull();
			response.Value!.Value.DownloadUrl.Should().Be(downloadUrl);
			response.Value!.Value.ImageId.Should().Be(imageId);
		}
    }

	private async Task StartPropagationServicesAsync<TCrdt, TDto>(IList<NodeMock> nodes) where TCrdt : ManagedCRDT<TDto>
    {
        var clients = new List<IDtoPassingGrpcService<TDto>>(nodes.Count);
        clients.AddRange(nodes.Select(node => new TestDtoPassingGrpcService<TCrdt, TDto>(node.Context)));

        foreach (var node in nodes)
        {
            var channelManagerMock = new Mock<IChannelManager>();
            for (var j = 0; j < nodes.Count; ++j)
            {
                var client = clients[j];
                var nodeId = nodes[j].Id;

                channelManagerMock.Setup(manager => manager.TryGet(nodeId, out client)).Returns(true);
            }

            var propagationService = new PropagationService<TCrdt, TDto>(node.Context,
                new NextInRingPropagationStrategy(),
                channelManagerMock.Object,
                node.QueueProvider,
                node.InfoProvider.GetMyNodeInfo(),
                _output.BuildLoggerFor<PropagationService<TCrdt, TDto>>());
            await propagationService.StartAsync(CancellationToken.None);
            _hostedServices.Add(propagationService);
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task DisposeAsync() => Task.WhenAll(_hostedServices.Select(service => service.StopAsync(CancellationToken.None)));
}