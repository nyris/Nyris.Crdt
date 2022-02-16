using System;
using System.Collections.Generic;
using System.Linq;
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
        public override async Task<CollectionIdMessage> CreateImagesCollection(Collection request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateImagesCollection));
            var id = string.IsNullOrEmpty(request.Id) ? CollectionId.New() : CollectionId.Parse(request.Id);
            var collection = new ImageInfoLwwCollection(id.ToString());

            var added = await _context.ImageCollectionsRegistry.TryAddAsync(id, _thisNodeId, collection,
                waitForPropagationToNumNodes: 3,
                traceId: request.TraceId,
                cancellationToken: context.CancellationToken);
            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(CreateImagesCollection));
            return new CollectionIdMessage {Id = added ? id.ToString() : "", TraceId = request.TraceId};
        }

        /// <inheritdoc />
        public override async Task<CollectionIdMessage> CreateImagesCollectionPR(Collection request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateImagesCollectionPR));
            var id = string.IsNullOrEmpty(request.Id) ? CollectionId.New() : CollectionId.Parse(request.Id);
            var added = await _context.PartiallyReplicatedImageCollectionsRegistry.TryAddCollectionAsync(id, id.ToString(),
                new [] { ImageIdIndex.IndexName },
                waitForPropagationToNumNodes: 2,
                traceId: request.TraceId,
                cancellationToken: context.CancellationToken);
            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(CreateImagesCollectionPR));
            return new CollectionIdMessage {Id = added ? id.ToString() : "", TraceId = request.TraceId};
        }

        /// <inheritdoc />
        public override async Task<Collection> GetCollectionInfo(CollectionIdMessage request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetCollectionInfo));
            if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.Id), out var collection))
            {
                ThrowNotFound($"Collection with id '{request.Id}' not found");
            }

            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetCollectionInfo));
            return new Collection { Size = (ulong) collection!.Values.Count(), Id = request.Id, TraceId = request.TraceId };
        }

        /// <inheritdoc />
        public override Task<Collection> GetCollectionInfoPR(CollectionIdMessage request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetCollectionInfoPR));
            if (!_context.PartiallyReplicatedImageCollectionsRegistry.TryGetCollectionSize(
                    CollectionId.Parse(request.Id), out var size))
            {
                ThrowNotFound($"Collection with id '{request.Id}' not found");
            }
            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetCollectionInfoPR));
            return Task.FromResult(new Collection { Id = request.Id, Size = size, TraceId = request.TraceId});
        }

        /// <inheritdoc />
        public override async Task<Image> CreateImage(Image request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateImage));
            if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.CollectionId), out var collection))
            {
                ThrowNotFound($"Collection with id '{request.CollectionId}' not found");
            }

            var imageGuid = string.IsNullOrEmpty(request.Guid) ? ImageGuid.New() : ImageGuid.Parse(request.Guid);
            var imageInfo = new ImageInfo(new Uri(request.DownloadUri), Convert.ToHexString(request.Id.Span));

            await collection!.SetAsync(imageGuid,
                imageInfo,
                DateTime.UtcNow,
                propagateToNodes: 2,
                traceId: request.TraceId,
                cancellationToken: context.CancellationToken);
            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(CreateImage));
            return new Image(request) { Guid = imageGuid.ToString() };
        }

        /// <inheritdoc />
        public override async Task<Image> CreateImagePR(Image request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateImagePR));
            var imageGuid = string.IsNullOrEmpty(request.Guid) ? ImageGuid.New() : ImageGuid.Parse(request.Guid);
            var imageInfo = new ImageInfo(new Uri(request.DownloadUri), Convert.ToHexString(request.Id.Span));

            var result = await _context.PartiallyReplicatedImageCollectionsRegistry
                .ApplyAsync<AddValueOperation<ImageGuid, ImageInfo, DateTime>, ValueResponse<ImageInfo>>(
                    CollectionId.Parse(request.CollectionId),
                    new AddValueOperation<ImageGuid, ImageInfo, DateTime>(imageGuid, imageInfo, DateTime.UtcNow),
                    traceId: request.TraceId,
                    cancellationToken: context.CancellationToken);

            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(CreateImagePR));
            return result.Value != default
                ? new Image(request) { Guid = imageGuid.ToString()}
                : new Image();
        }

        /// <inheritdoc />
        public override async Task<Image> GetImage(ImageUuids request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetImage));
            if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.CollectionId),
                    out var collection))
            {
                ThrowNotFound($"Collection with id '{request.CollectionId}' not found");
            }

            if (!collection!.TryGetValue(ImageGuid.Parse(request.ImageUuid), out var image))
            {
                ThrowNotFound($"Image with id '{request.ImageUuid}' not found");
            }

            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetImage));
            return new Image
            {
                Guid = request.ImageUuid,
                CollectionId = request.CollectionId,
                Id = ByteString.CopyFrom(Convert.FromHexString(image!.ImageId)),
                DownloadUri = image.DownloadUrl.ToString(),
                TraceId = request.TraceId
            };
        }

        /// <inheritdoc />
        public override async Task<Image> GetImagePR(ImageUuids request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetImagePR));
            var result = await _context.PartiallyReplicatedImageCollectionsRegistry
                .ApplyAsync<GetValueOperation<ImageGuid>, ValueResponse<ImageInfo>>(
                    CollectionId.Parse(request.CollectionId),
                    new GetValueOperation<ImageGuid>(ImageGuid.Parse(request.ImageUuid)), traceId:
                    request.TraceId);

            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetImagePR));
            return new Image
            {
                Guid = request.ImageUuid,
                CollectionId = request.CollectionId,
                Id = ByteString.CopyFrom(Convert.FromHexString(result.Value?.ImageId ?? "")),
                DownloadUri = result.Value?.DownloadUrl.ToString(),
                TraceId = request.TraceId
            };
        }

        /// <inheritdoc />
        public override async Task<ImageUuidList> FindImagePR(FindImageById request, ServerCallContext context)
        {
            _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(FindImagePR));
            var result = await _context.PartiallyReplicatedImageCollectionsRegistry
                .ApplyAsync<FindIdsOperation, ValueResponse<IList<ImageGuid>>>(
                    CollectionId.Parse(request.CollectionId),
                    new FindIdsOperation(Convert.ToHexString(request.Id.Span)),
                    request.TraceId,
                    context.CancellationToken);

            _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(FindImagePR));
            return new ImageUuidList
            {
                CollectionId = request.CollectionId,
                TraceId = request.TraceId,
                ImageUuid = { result.Value.Select(i => i.ToString()) }
            };
        }

        /// <inheritdoc />
        public override async Task<BoolResponse> DeleteCollection(CollectionIdMessage request, ServerCallContext context)
        {
            await _context.ImageCollectionsRegistry.RemoveAsync(CollectionId.Parse(request.Id),
                cancellationToken: context.CancellationToken);
            return new BoolResponse { Value = true };
        }

        /// <inheritdoc />
        public override async Task<BoolResponse> DeleteCollectionPR(CollectionIdMessage request, ServerCallContext context)
        {
            return new BoolResponse
            {
                Value = await _context.PartiallyReplicatedImageCollectionsRegistry.TryRemoveCollectionAsync(CollectionId.Parse(request.Id),
                    cancellationToken: context.CancellationToken)
            };
        }

        /// <inheritdoc />
        public override async Task<BoolResponse> ImagesCollectionExists(CollectionIdMessage request, ServerCallContext context) =>
            new()
            {
                Value = _context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.Id), out _)
            };

        /// <inheritdoc />
        public override async Task<BoolResponse> ImagesCollectionExistsPR(CollectionIdMessage request, ServerCallContext context) =>
            new()
            {
                Value = _context.PartiallyReplicatedImageCollectionsRegistry
                    .CollectionExists(CollectionId.Parse(request.Id))
            };

        private static void ThrowNotFound(string details) => throw new RpcException(new Status(StatusCode.NotFound, details));
    }
}
