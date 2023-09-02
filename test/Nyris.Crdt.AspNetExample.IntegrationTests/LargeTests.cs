using FluentAssertions;
using Google.Protobuf;
using Grpc.Net.Client;
using Nyris.Common;
using Nyris.Crdt.AspNetExample;
using Nyris.Extensions.Guids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.GrpcServiceSample.IntegrationTests;

public sealed class LargeTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly GrpcChannel _channelA;
    private readonly Api.ApiClient _clientA;

    private readonly GrpcChannel _channelB;
    private readonly Api.ApiClient _clientB;

    private readonly GrpcChannel _channelC;
    private readonly Api.ApiClient _clientC;

    public LargeTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _channelA = GrpcChannel.ForAddress("http://localhost:5001");
        _clientA = new Api.ApiClient(_channelA);
        _channelB = GrpcChannel.ForAddress("http://localhost:5011");
        _clientB = new Api.ApiClient(_channelB);
        _channelC = GrpcChannel.ForAddress("http://localhost:5021");
        _clientC = new Api.ApiClient(_channelC);
    }

    private int _counter;

    private Api.ApiClient RandomClient => (Interlocked.Increment(ref _counter) % 3) switch
    {
        0 => _clientA,
        1 => _clientB,
        2 => _clientC,
        _ => throw new ArgumentOutOfRangeException()
    };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    [InlineData(10, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(10, 1)]
    public async Task AddingLotsOfImagesWorks(uint numShards, uint propagateToNodes)
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);

        var collectionIdMessage1 = await CreateAndPopulateCollectionAsync(200, 30, traceId, numShards, propagateToNodes);
        (await CollectionSizeShouldEventuallyBeAsync(_clientA, collectionIdMessage1, 170, TimeSpan.FromSeconds(60)))
            .Should().BeTrue();

        var collectionIdMessage2 = await CreateAndPopulateCollectionAsync(2000, 300, traceId, numShards, propagateToNodes);
        (await CollectionSizeShouldEventuallyBeAsync(_clientA, collectionIdMessage2, 1700, TimeSpan.FromSeconds(120)))
            .Should().BeTrue();

        var collectionIdMessage3 = await CreateAndPopulateCollectionAsync(10000, 5000, traceId, numShards, propagateToNodes);
        (await CollectionSizeShouldEventuallyBeAsync(_clientA, collectionIdMessage3, 5000, TimeSpan.FromSeconds(120)))
            .Should().BeTrue();

        await _clientA.DeleteCollectionPRAsync(collectionIdMessage1);
        await _clientA.DeleteCollectionPRAsync(collectionIdMessage2);
        await _clientA.DeleteCollectionPRAsync(collectionIdMessage3);
    }

    private async Task<CollectionIdMessage> CreateAndPopulateCollectionAsync(int numImages,
        int nDeletedImages,
        string traceId,
        uint numShards,
        uint propagateToNodes)
    {
        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
        });

        var images = new List<Image>(numImages);
        var counter = 0;
        var outputStep = numImages / 5;
        var startTime = DateTime.Now;

        await foreach (var image in GenerateImages(collection.Id, numImages, propagateToNodes)
                           .ProcessInBatch(async (i, token) =>
                           {
                               var image = await RandomClient.CreateImagePRAsync(i, cancellationToken: token);
                               image.DownloadUri.Should().Be(i.Image.DownloadUri, $"TraceId: {i.TraceId}");
                               var c = Interlocked.Increment(ref counter);

                               if (c % outputStep == 0)
                               {
                                   var timePassed = DateTime.Now - startTime;
                                   startTime = DateTime.Now;
                                   _testOutputHelper.WriteLine($"{c} out of {numImages} images created, " +
                                                               $"{outputStep / timePassed.TotalSeconds:.#} img/s");
                               }

                               return image;
                           }, batchSize: 20))
        {
            images.Add(await image);
        }

        var deletedImages = new List<Image>(nDeletedImages);
        for (var i = 0; i < nDeletedImages; ++i)
        {
            var k = Random.Shared.Next(0, images.Count);
            deletedImages.Add(images[k]);
            images.RemoveAt(k);
        }

        counter = 0;
        outputStep = nDeletedImages / 5;
        startTime = DateTime.Now;

        await deletedImages
            .Select(i => new DeleteImageRequest
            {
                CollectionId = i.CollectionId,
                ImageUuid = i.Guid,
                PropagateToNodes = propagateToNodes
            })
            .ProcessInBatch(async (i, token) =>
            {
                var r = await RandomClient.DeleteImagePRAsync(i, cancellationToken: token);
                r.Value.Should().BeTrue();
                var c = Interlocked.Increment(ref counter);
                if (c % outputStep == 0)
                {
                    var timePassed = DateTime.Now - startTime;
                    startTime = DateTime.Now;
                    _testOutputHelper.WriteLine($"{c} out of {nDeletedImages} images deleted, " +
                                                $"{outputStep / timePassed.TotalSeconds:.#} img/s");
                }
            }, batchSize: 20);

        return new CollectionIdMessage { Id = collection.Id, TraceId = traceId };
    }

    private async Task<bool> CollectionSizeShouldEventuallyBeAsync(Api.ApiClient client, CollectionIdMessage collectionIdMessage,
        ulong size, TimeSpan maxDelay)
    {
        var start = DateTime.Now;
        while (start.Add(maxDelay) > DateTime.Now)
        {
            var info = await client.GetCollectionInfoPRAsync(collectionIdMessage);
            if (info.Size == size)
            {
                _testOutputHelper.WriteLine($"Success! Collection {collectionIdMessage.Id} is of size {size}");
                return true;
            }

            _testOutputHelper.WriteLine($"Collection {collectionIdMessage.Id} size was expected to be {size} but found {info.Size}");
            await Task.Delay(maxDelay / 10);
        }

        return false;
    }

    private IEnumerable<CreateImageMessage> GenerateImages(string collectionId, int n, uint propagateToNodes, string traceId = "")
    {
        var imageId = new byte[32];
        for (var i = 0; i < n; ++i)
        {
            Random.Shared.NextBytes(imageId);
            yield return new CreateImageMessage
            {
                Image = new Image
                {
                    DownloadUri = $"https://{i}-url-looking.string/",
                    Id = ByteString.CopyFrom(imageId),
                    CollectionId = collectionId
                },
                TraceId = string.IsNullOrWhiteSpace(traceId) ? ShortGuid.NewGuid().ToString() : traceId,
                PropagateToNodes = propagateToNodes
            };
        }
    }
}
