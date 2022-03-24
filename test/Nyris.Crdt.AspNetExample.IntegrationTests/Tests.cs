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
using Xunit.Sdk;

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
        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId
        });

        await AddRandomImagesPartiallyReplicatedAsync(_clientA, collection.Id, 10, traceId);
        await AddRandomImagesPartiallyReplicatedAsync(_clientB, collection.Id, 10, traceId);
        await AddRandomImagesPartiallyReplicatedAsync(_clientC, collection.Id, 10, traceId);

        var tasks = new[]
        {
            CollectionGrownToSizeWithinTimeAsync(_clientA, collection, 30, TimeSpan.FromSeconds(120)),
            CollectionGrownToSizeWithinTimeAsync(_clientB, collection, 30, TimeSpan.FromSeconds(120)),
            CollectionGrownToSizeWithinTimeAsync(_clientC, collection, 30, TimeSpan.FromSeconds(120))
        };
        await Task.WhenAll(tasks);
        tasks.Select(t => t.Result).Should().OnlyContain(result => result == true);

        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });

        async Task<bool> CollectionGrownToSizeWithinTimeAsync(Api.ApiClient client,
            CollectionIdMessage collectionIdMsg,
            ulong expectedSize,
            TimeSpan allowedDelay)
        {
            var start = DateTime.Now;
            var delay = allowedDelay / 20;
            while (start.Add(allowedDelay) > DateTime.Now)
            {
                var collectionInfo = await client.GetCollectionInfoPRAsync(collectionIdMsg);
                collectionInfo.Id.Should().Be(collectionIdMsg.Id);
                if (collectionInfo.Size == expectedSize) return true;
                await Task.Delay(delay);
            }

            return false;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CreateCollectionPartiallyReplicatedWorks(uint numShards)
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var response = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
        });
        response.Id.Should().NotBeEmpty();
        var exists = await _clientA.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = response.Id, TraceId = traceId });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CreateCollectionPartiallyReplicatedPropagationWorks(uint numShards)
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var response = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
        }, deadline:DateTime.UtcNow.AddSeconds(5));
        response.Id.Should().NotBeEmpty();

        var exists = await _clientB.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();

        exists = await _clientC.ImagesCollectionExistsPRAsync(response);
        exists.Value.Should().BeTrue();
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = response.Id, TraceId = traceId });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CreateImagePartiallyReplicatedWorks(uint numShards)
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CreateImagePartiallyReplicatedPropagationWorks(uint numShards)
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task DeleteImagePartiallyReplicatedPropagationWorks(uint numShards)
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var imageId = new byte[] { 1, 2, 3, 4 };
        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
        });

        var image = await _clientB.CreateImagePRAsync(new Image
        {
            DownloadUri = "https://url-looking.string/",
            Id = ByteString.CopyFrom(imageId),
            CollectionId = collection.Id,
            TraceId = traceId
        });

        image.Guid.Should().NotBeEmpty();
        image.CollectionId.Should().Be(collection.Id);

        var response = await _clientC.DeleteImagePRAsync(new ImageUuids
        {
            ImageUuid = image.Guid,
            CollectionId = collection.Id,
            TraceId = traceId
        });

        response.Value.Should().BeTrue();
        await _clientA.DeleteCollectionPRAsync(new CollectionIdMessage { Id = collection.Id, TraceId = traceId });
    }

    [Fact]
    public async Task FindImagesByIdWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);
        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = 3
        });

        var images = await AddRandomImagesPartiallyReplicatedAsync(_clientB, collection.Id, 2, traceId);
        // create a copy of first image, so that we can find more than one image later
        var duplicateImage = await _clientC.CreateImagePRAsync(new Image
        {
            Id = images[0].Id,
            CollectionId = collection.Id,
            DownloadUri = "http://whatever",
            TraceId = traceId
        });

        var tasks = new[]
        {
            ShouldEventuallyFindAsync(_clientA,
                new FindImageById
                {
                    CollectionId = collection.Id,
                    Id = duplicateImage.Id,
                    TraceId = traceId
                }, new[] { duplicateImage.Guid, images[0].Guid },
                TimeSpan.FromSeconds(30)),
            ShouldEventuallyFindAsync(_clientA,
                new FindImageById
                {
                    CollectionId = collection.Id,
                    Id = images[1].Id,
                    TraceId = traceId
                }, new[] { images[1].Guid },
                TimeSpan.FromSeconds(30))
        };

        await Task.WhenAll(tasks);
        tasks.Select(t => t.Result).Should().OnlyContain(result => result == true);

        async Task<bool> ShouldEventuallyFindAsync(Api.ApiClient client,
            FindImageById request,
            string[] expectedGuids,
            TimeSpan maxDelay)
        {
            var start = DateTime.Now;
            while (start.Add(maxDelay) > DateTime.Now)
            {
                try
                {
                    var ids = await client.FindImagePRAsync(request);
                    ids.ImageUuid.Should().BeEquivalentTo(expectedGuids,
                        options => options.WithoutStrictOrdering());
                    return true;
                }
                catch (XunitException)
                {
                    await Task.Delay(maxDelay / 10);
                }
            }

            return false;
        }
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

    private static async Task<IList<Image>> AddRandomImagesPartiallyReplicatedAsync(Api.ApiClient client, string collectionId, int n, string traceId)
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