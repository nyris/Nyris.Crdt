using System;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Net.Client;
using Nyris.Crdt.AspNetExample;
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
        _channelB = GrpcChannel.ForAddress("http://localhost:6001");
        _clientB = new Api.ApiClient(_channelB);
        _channelC = GrpcChannel.ForAddress("http://localhost:7001");
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
        var response = await _clientA.CreateImagesCollectionAsync(new Collection());
        response.Id.Should().NotBeEmpty();
        var exists = await _clientA.ImagesCollectionExistsAsync(response);
        exists.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCollectionPropagationWorks()
    {
        var response = await _clientA.CreateImagesCollectionAsync(new Collection());
        response.Id.Should().NotBeEmpty();
        // await Task.Delay(TimeSpan.FromSeconds(3));

        var exists = await _clientB.ImagesCollectionExistsAsync(response);
        exists.Value.Should().BeTrue();

        exists = await _clientC.ImagesCollectionExistsAsync(response);
        exists.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CreateImageWorks()
    {
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionAsync(new Collection());
        var image = await _clientA.CreateImageAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var retrievedImage = await _clientA.GetImageAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id
        });

        retrievedImage.Should().BeEquivalentTo(image);
    }

    [Fact]
    public async Task CreateImagePropagationWorks()
    {
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionAsync(new Collection());
        // await Task.Delay(TimeSpan.FromSeconds(1));

        var image = await _clientB.CreateImageAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        await Task.Delay(TimeSpan.FromSeconds(3));
        var retrievedImage = await _clientC.GetImageAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id
        });

        retrievedImage.Should().BeEquivalentTo(image);
    }

    [Fact]
    public async Task CreateCollectionPRWorks()
    {
        var response = await _clientA.CreateImagesCollectionPRAsync(new Collection());
        response.Id.Should().NotBeEmpty();
        var exists = await _clientA.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCollectionPRPropagationWorks()
    {
        var response = await _clientA.CreateImagesCollectionPRAsync(new Collection());
        response.Id.Should().NotBeEmpty();
        // await Task.Delay(TimeSpan.FromSeconds(10));

        var exists = await _clientB.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();

        exists = await _clientC.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CreateImagePRWorks()
    {
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new Collection());
        var image = await _clientA.CreateImagePRAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var retrievedImage = await _clientA.GetImagePRAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id
        });

        retrievedImage.Should().BeEquivalentTo(image);
    }

    [Fact]
    public async Task CreateImagePRPropagationWorks()
    {
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new Collection());
        // await Task.Delay(TimeSpan.FromSeconds(1));

        var image = await _clientB.CreateImagePRAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        await Task.Delay(TimeSpan.FromSeconds(3));
        var retrievedImage = await _clientC.GetImagePRAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id
        });

        retrievedImage.Should().BeEquivalentTo(image);
    }

    public void Dispose()
    {
        _channelA.Dispose();
        _channelB.Dispose();
        _channelC.Dispose();
        GC.SuppressFinalize(this);
    }
}