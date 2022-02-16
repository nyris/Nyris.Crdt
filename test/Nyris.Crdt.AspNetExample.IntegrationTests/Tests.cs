using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Net.Client;
using Nyris.Crdt.AspNetExample;
using Nyris.Extensions.Guids;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.GrpcServiceSample.IntegrationTests;

public class Tests : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly GrpcChannel _channelA;
    private readonly Api.ApiClient _clientA;

    private readonly GrpcChannel _channelB;
    private readonly Api.ApiClient _clientB;

    private readonly GrpcChannel _channelC;
    private readonly Api.ApiClient _clientC;

    public Tests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _channelA = GrpcChannel.ForAddress("http://localhost:5001");
        _clientA = new Api.ApiClient(_channelA);
        _channelB = GrpcChannel.ForAddress("http://localhost:5011");
        _clientB = new Api.ApiClient(_channelB);
        _channelC = GrpcChannel.ForAddress("http://localhost:5021");
        _clientC = new Api.ApiClient(_channelC);
    }

    /// <summary>
    /// Tests that all instances are available and able to provide a gRPC api on expected ports.
    /// </summary>
    [Fact]
    public async Task GreetingsTest()
    {
        await Task.WhenAll(TestClient(_clientA), TestClient(_clientB), TestClient(_clientC));

        async Task TestClient(Api.ApiClient client)
        {
            const string name = "Integration Test";
            var response = await client.SayHelloAsync(new HelloRequest { Name = name });
            response.Message.Should().Be($"Hello {name}");
        }
    }

    [Fact]
    public async Task CreateCollectionWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var response = await _clientA.CreateImagesCollectionAsync(new Collection
        {
            TraceId = traceId
        });
        response.Id.Should().NotBeEmpty();
        var exists = await _clientA.ImagesCollectionExistsAsync(response);
        exists.Value.Should().BeTrue();
        await _clientA.DeleteCollectionAsync(new CollectionIdMessage { Id = response.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateCollectionPropagationWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var response = await _clientA.CreateImagesCollectionAsync(new Collection
        {
            TraceId = traceId
        });
        response.Id.Should().NotBeEmpty();

        var exists = await _clientB.ImagesCollectionExistsAsync(response);
        exists.Value.Should().BeTrue();

        exists = await _clientC.ImagesCollectionExistsAsync(response);
        exists.Value.Should().BeTrue();
        await _clientA.DeleteCollectionAsync(new CollectionIdMessage { Id = response.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateImageWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionAsync(new Collection
        {
            TraceId = traceId
        });
        var image = await _clientA.CreateImageAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
            TraceId = traceId
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var retrievedImage = await _clientA.GetImageAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id,
            TraceId = traceId
        });

        retrievedImage.Should().BeEquivalentTo(image);
        await _clientA.DeleteCollectionAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateImagePropagationWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionAsync(new Collection
        {
            TraceId = traceId
        });
        // await Task.Delay(TimeSpan.FromSeconds(1));

        var image = await _clientB.CreateImageAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
            TraceId = traceId
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var retrievedImage = await _clientC.GetImageAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id,
            TraceId = traceId
        });

        retrievedImage.Should().BeEquivalentTo(image);
        await _clientA.DeleteCollectionAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task GetCollectionSizePropagationWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var collection = await _clientA.CreateImagesCollectionAsync(new Collection
        {
            TraceId = traceId
        });

        await AddRandomImagesAsync(_clientA, collection.Id, 10, traceId);
        await AddRandomImagesAsync(_clientB, collection.Id, 10, traceId);
        await AddRandomImagesAsync(_clientC, collection.Id, 10, traceId);

        var collectionInfo = await _clientA.GetCollectionInfoAsync(collection);
        collectionInfo.Id.Should().Be(collection.Id);
        collectionInfo.Size.Should().Be(30);

        collectionInfo = await _clientB.GetCollectionInfoAsync(collection);
        collectionInfo.Id.Should().Be(collection.Id);
        collectionInfo.Size.Should().Be(30);

        collectionInfo = await _clientC.GetCollectionInfoAsync(collection);
        collectionInfo.Id.Should().Be(collection.Id);
        collectionInfo.Size.Should().Be(30);

        await _clientA.DeleteCollectionAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task GetCollectionSizePropagationPRWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var collection = await _clientA.CreateImagesCollectionPRAsync(new Collection
        {
            TraceId = traceId
        });

        await AddRandomImagesPRAsync(_clientB, collection.Id, 10, traceId);
        await AddRandomImagesPRAsync(_clientC, collection.Id, 10, traceId);
        await AddRandomImagesPRAsync(_clientA, collection.Id, 10, traceId);

        var collectionInfo = await _clientA.GetCollectionInfoPRAsync(collection);
        collectionInfo.Id.Should().Be(collection.Id);
        collectionInfo.Size.Should().Be(30);

        collectionInfo = await _clientB.GetCollectionInfoPRAsync(collection);
        collectionInfo.Id.Should().Be(collection.Id);
        collectionInfo.Size.Should().Be(30);

        collectionInfo = await _clientC.GetCollectionInfoPRAsync(collection);
        collectionInfo.Id.Should().Be(collection.Id);
        collectionInfo.Size.Should().Be(30);
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateCollectionPRWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var response = await _clientA.CreateImagesCollectionPRAsync(new Collection
        {
            TraceId = traceId
        });
        response.Id.Should().NotBeEmpty();
        var exists = await _clientA.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = response.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateCollectionPRPropagationWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var response = await _clientA.CreateImagesCollectionPRAsync(new Collection
        {
            TraceId = traceId
        });
        response.Id.Should().NotBeEmpty();

        var exists = await _clientB.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();

        exists = await _clientC.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = response.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateImagePRWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new Collection
        {
            TraceId = traceId
        });
        var image = await _clientA.CreateImagePRAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
            TraceId = traceId
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var retrievedImage = await _clientA.GetImagePRAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id,
            TraceId = traceId
        });

        retrievedImage.Should().BeEquivalentTo(image);
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task CreateImagePRPropagationWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new Collection
        {
            TraceId = traceId
        });
        // await Task.Delay(TimeSpan.FromSeconds(1));

        var image = await _clientB.CreateImagePRAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
            TraceId = traceId
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var retrievedImage = await _clientC.GetImagePRAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id,
            TraceId = traceId
        });

        retrievedImage.Should().BeEquivalentTo(image);
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task FindImagesByIdWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var collection = await _clientA.CreateImagesCollectionPRAsync(new Collection
        {
            TraceId = traceId
        });

        var images = await AddRandomImagesPRAsync(_clientB, collection.Id, 2, traceId);
        // create a copy of first image, so that we can find more than one image later
        var duplicateImage = await _clientC.CreateImagePRAsync(new Image
        {
            Id = images[0].Id,
            CollectionId = collection.Id,
            DownloadUri = "http://whatever",
            TraceId = traceId
        });

        var ids = await _clientA.FindImagePRAsync(new FindImageById
        {
            CollectionId = collection.Id,
            Id = duplicateImage.Id,
            TraceId = traceId
        });

        ids.ImageUuid.Should().HaveCount(2);
        ids.ImageUuid.Should().Contain(duplicateImage.Guid).And.Contain(images[0].Guid);

        ids = await _clientA.FindImagePRAsync(new FindImageById
        {
            CollectionId = collection.Id,
            Id = images[1].Id,
            TraceId = traceId
        });

        ids.ImageUuid.Should().HaveCount(1);
        ids.ImageUuid.Should().ContainSingle(images[1].Guid);
    }

    public void Dispose()
    {
        _channelA.Dispose();
        _channelB.Dispose();
        _channelC.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<IList<Image>> AddRandomImagesAsync(Api.ApiClient client, string collectionId, int n, string traceId)
    {
        var ids = new List<Image>(n);
        var imageIdBuffer = new byte[32];
        for (var i = 0; i < n; ++i)
        {
            Random.Shared.NextBytes(imageIdBuffer);
            var image = await client.CreateImageAsync(new Image
            {
                DownloadUri = $"https://url-looking.string/{i}",
                Id = ByteString.CopyFrom(imageIdBuffer),
                CollectionId = collectionId,
                TraceId = traceId
            });

            image.Guid.Should().NotBeEmpty();
            ids.Add(image);
        }

        return ids;
    }

    private static async Task<IList<Image>> AddRandomImagesPRAsync(Api.ApiClient client, string collectionId, int n, string traceId)
    {
        var ids = new List<Image>(n);
        var imageIdBuffer = new byte[32];
        for (var i = 0; i < n; ++i)
        {
            Random.Shared.NextBytes(imageIdBuffer);
            var image = await client.CreateImagePRAsync(new Image
            {
                DownloadUri = $"https://url-looking.string/{i}",
                Id = ByteString.CopyFrom(imageIdBuffer),
                CollectionId = collectionId,
                TraceId = traceId
            });

            image.Guid.Should().NotBeEmpty();
            ids.Add(image);
        }

        return ids;
    }
}