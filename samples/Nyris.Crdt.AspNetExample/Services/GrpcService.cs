using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.AspNetExample.Services
{
    public class GrpcService : Api.ApiBase
    {
        private readonly ILogger<GrpcService> _logger;
        private readonly MyContext _context;
        private readonly NodeId _thisNodeId;

        public GrpcService(ILogger<GrpcService> logger, MyContext context, NodeInfo thisNode)
        {
            _logger = logger;
            _context = context;
            _thisNodeId = thisNode.Id;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }

        /// <inheritdoc />
        public override async Task<Collection> CreateImagesCollection(Collection request, ServerCallContext context)
        {
            var id = string.IsNullOrEmpty(request.Id) ? CollectionId.New() : CollectionId.Parse(request.Id);
            var collection = new ImageInfoLwwCollection(id.ToString());

            var added = await _context.ImageCollectionsRegistry.TryAddAsync(id, _thisNodeId, collection, 2);
            return added ? new Collection { Id = id.ToString() } : new Collection();
        }

        /// <inheritdoc />
        public override async Task<Collection> CreateImagesCollectionPR(Collection request, ServerCallContext context)
        {
            var id = string.IsNullOrEmpty(request.Id) ? CollectionId.New() : CollectionId.Parse(request.Id);
            var collection = new ImageInfoLwwCollectionWithSerializableOperations(id.ToString());

            var added = await _context.PartiallyReplicatedImageCollectionsRegistry.TryAddCollectionAsync(id,
                collection,
                2);
            return added ? new Collection { Id = id.ToString() } : new Collection();
        }

        /// <inheritdoc />
        public override async Task<Image> CreateImage(Image request, ServerCallContext context)
        {
            if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.CollectionId), out var collection))
            {
                ThrowNotFound($"Collection with id '{request.CollectionId}' not found");
            }

            var imageGuid = string.IsNullOrEmpty(request.Guid) ? ImageGuid.New() : ImageGuid.Parse(request.Guid);
            var imageInfo = new ImageInfo(new Uri(request.DownloadUri), Convert.ToHexString(request.Id.Span));

            return collection!.TrySet(imageGuid, imageInfo, DateTime.UtcNow, out _)
                ? new Image(request) { Guid = imageGuid.ToString() }
                : new Image();
        }

        /// <inheritdoc />
        public override async Task<Image> CreateImagePR(Image request, ServerCallContext context)
        {
            var imageGuid = string.IsNullOrEmpty(request.Guid) ? ImageGuid.New() : ImageGuid.Parse(request.Guid);
            var imageInfo = new ImageInfo(new Uri(request.DownloadUri), Convert.ToHexString(request.Id.Span));

            var result = await _context.PartiallyReplicatedImageCollectionsRegistry
                .ApplyAsync<AddValueOperation<ImageGuid, ImageInfo, DateTime>, ValueResponse<ImageInfo>>(
                    CollectionId.Parse(request.CollectionId),
                    new AddValueOperation<ImageGuid, ImageInfo, DateTime>(imageGuid, imageInfo, DateTime.UtcNow));

            return result.Value != default
                ? new Image(request) { Guid = imageGuid.ToString() }
                : new Image();
        }

        /// <inheritdoc />
        public override async Task<Image> GetImage(ImageUuids request, ServerCallContext context)
        {
            if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.CollectionId),
                    out var collection))
            {
                ThrowNotFound($"Collection with id '{request.CollectionId}' not found");
            }

            if (!collection!.TryGetValue(ImageGuid.Parse(request.ImageUuid), out var image))
            {
                ThrowNotFound($"Image with id '{request.ImageUuid}' not found");
            }

            return new Image
            {
                Guid = request.ImageUuid,
                CollectionId = request.CollectionId,
                Id = ByteString.CopyFrom(Convert.FromHexString(image!.ImageId)),
                DownloadUri = image.DownloadUrl.ToString()
            };
        }

        /// <inheritdoc />
        public override async Task<Image> GetImagePR(ImageUuids request, ServerCallContext context)
        {
            var result = await _context.PartiallyReplicatedImageCollectionsRegistry
                .ApplyAsync<GetValueOperation<ImageGuid>, ValueResponse<ImageInfo>>(
                    CollectionId.Parse(request.CollectionId),
                    new GetValueOperation<ImageGuid>(ImageGuid.Parse(request.ImageUuid)));

            return new Image
            {
                Guid = request.ImageUuid,
                CollectionId = request.CollectionId,
                Id = ByteString.CopyFrom(Convert.FromHexString(result.Value?.ImageId ?? "")),
                DownloadUri = result.Value?.DownloadUrl.ToString()
            };
        }

        /// <inheritdoc />
        public override async Task<BoolResponse> ImagesCollectionExists(Collection request, ServerCallContext context) =>
            new()
            {
                Value = _context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.Id), out _)
            };

        /// <inheritdoc />
        public override async Task<BoolResponse> ImagesCollectionExistsPR(Collection request, ServerCallContext context) =>
            new()
            {
                Value = _context.PartiallyReplicatedImageCollectionsRegistry
                    .CollectionExists(CollectionId.Parse(request.Id))
            };

        private static void ThrowNotFound(string details) => throw new RpcException(new Status(StatusCode.NotFound, details));
    }
}
