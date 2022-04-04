using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Net.Client;
using Nyris.Common;
using Nyris.Crdt.AspNetExample;
using Nyris.Extensions.Guids;
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
        2 => _clientC
    };

    [Fact]
    public async Task AddingLotsOfImagesWorks()
    {
        const int numShards = 3;
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);

        var collection = await _clientA.CreateImagesCollectionPRAsync(new ShardedCollection
        {
            TraceId = traceId,
            NumShards = numShards
        });

        const int numImages = 200;
        const int nDeletedImages = 30;
        var images = new List<Image>(numImages);
        await foreach (var image in GenerateImages(collection.Id, numImages, traceId)
                           .ProcessInBatch(async (i, token) => await RandomClient
                               .CreateImagePRAsync(i, cancellationToken: token), batchSize: 10))
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

        await deletedImages
            .Select(i => new ImageUuids { CollectionId = i.CollectionId, ImageUuid = i.Guid, TraceId = i.TraceId })
            .ProcessInBatch<ImageUuids>(async (i, token) => await RandomClient
                .DeleteImagePRAsync(i, cancellationToken: token), batchSize: 10);

        var collectionIdMessage = new CollectionIdMessage { Id = collection.Id, TraceId = traceId };
        var info = await _clientA.GetCollectionInfoPRAsync(collectionIdMessage);
        info.Size.Should().Be(numImages - nDeletedImages);

        await _clientA.DeleteCollectionPRAsync(collectionIdMessage);
    }


    private IEnumerable<Image> GenerateImages(string collectionId, int n, string traceId = "")
    {
        var imageId = new byte[32];
        for (var i = 0; i < n; ++i)
        {
            Random.Shared.NextBytes(imageId);
            yield return new Image
            {
                DownloadUri = "https://url-looking.string/",
                Id = ByteString.CopyFrom(imageId),
                CollectionId = collectionId.ToString(),
                TraceId = traceId
            };
        }
    }
}