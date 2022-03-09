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
using Nyris.Crdt.Distributed.Grpc;
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
            var crdt = new ImageInfoCollectionsRegistry("1",
                queueProvider: node.QueueProvider,
                factory: new ImageInfoLwwCollection.ImageInfoLwwCollectionFactory(node.QueueProvider,
                    _output.BuildLogger()),
                logger: _output.BuildLogger());
            crdts.Add(crdt);
            node.Context.Add(crdt, ImageInfoCollectionsRegistry.DefaultFactory);
        }

        // add collection
        var collectionId = CollectionId.New();
        var collection = new ImageInfoLwwCollection(collectionId.ToString(),
            queueProvider: nodes[0].QueueProvider,
            logger: _output.BuildLogger());
        await crdts[0].TryAddAsync(collectionId, nodes[0].Id, collection, nNodes - 1);

        // add item to collection
        var imageUuid = ImageGuid.New();
        await collection.SetAsync(imageUuid, new ImageInfo(new Uri("http://test.url"), "1234"),
            DateTime.UtcNow, nNodes - 1);
        foreach (var crdt in crdts)
        {
            crdt.Size.Should().Be(1);
            crdt.TryGetValue(collectionId, out var extractedCollection).Should().BeTrue();
            extractedCollection!.Size.Should().Be(1);
            extractedCollection.Value.Should().ContainKey(imageUuid);
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