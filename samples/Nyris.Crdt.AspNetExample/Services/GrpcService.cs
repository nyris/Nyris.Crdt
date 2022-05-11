using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Model;
using Nyris.Extensions.Guids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nyris.Crdt.AspNetExample.Services;

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
    public override async Task<CollectionIdMessage> CreateImagesCollection(Collection request,
        ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateImagesCollection));
        var id = string.IsNullOrEmpty(request.Id) ? CollectionId.New() : CollectionId.Parse(request.Id);
        var collection = new ImageInfoLwwCollection(new InstanceId(id.ToString()));

        var added = await _context.ImageCollectionsRegistry.TryAddAsync(id, _thisNodeId, collection,
            propagationToNodes: 3,
            traceId: request.TraceId,
            cancellationToken: context.CancellationToken);
        _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(CreateImagesCollection));
        return new CollectionIdMessage { Id = added ? id.ToString() : "", TraceId = request.TraceId };
    }

    /// <inheritdoc />
    public override async Task<CollectionIdMessage> CreateImagesCollectionPR(ShardedCollection request,
        ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId,
            nameof(CreateImagesCollectionPR));
        var id = string.IsNullOrEmpty(request.Id) ? CollectionId.New() : CollectionId.Parse(request.Id);
        var added = await _context.PartiallyReplicatedImageCollectionsRegistry.TryAddCollectionAsync(id,
            new CollectionConfig
            {
                Name = id.ToString(),
                IndexNames = new[] { ImageIdIndex.IndexName },
                ShardingConfig = request.NumShards > 0
                    ? new ShardingConfig { NumShards = (ushort) request.NumShards }
                    : null
            },
            propagateToNodes: 3,
            traceId: request.TraceId,
            cancellationToken: context.CancellationToken);
        _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId,
            nameof(CreateImagesCollectionPR));
        return new CollectionIdMessage { Id = added ? id.ToString() : "", TraceId = request.TraceId };
    }

    /// <inheritdoc />
    public override Task<Collection> GetCollectionInfo(CollectionIdMessage request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetCollectionInfo));
        if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.Id), out var collection))
        {
            ThrowNotFound($"Collection with id '{request.Id}' not found");
        }

        _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetCollectionInfo));
        return Task.FromResult(new Collection
            { Size = (ulong) collection!.Values.Count(), Id = request.Id, TraceId = request.TraceId });
    }

    /// <inheritdoc />
    public override Task<Collection> GetCollectionInfoPR(CollectionIdMessage request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetCollectionInfoPR));
        if (!_context.PartiallyReplicatedImageCollectionsRegistry.TryGetCollectionSize(
                CollectionId.Parse(request.Id), out var size, out _))
        {
            ThrowNotFound($"Collection with id '{request.Id}' not found");
        }

        _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetCollectionInfoPR));
        return Task.FromResult(new Collection { Id = request.Id, Size = size, TraceId = request.TraceId });
    }

    /// <inheritdoc />
    public override async Task<Image> CreateImage(CreateImageMessage request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateImage));
        if (!_context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.Image.CollectionId),
                out var collection))
        {
            ThrowNotFound($"Collection with id '{request.Image.CollectionId}' not found");
        }

        var imageGuid = string.IsNullOrEmpty(request.Image.Guid)
            ? ImageGuid.New()
            : ImageGuid.Parse(request.Image.Guid);
        var imageInfo = new ImageInfo(new Uri(request.Image.DownloadUri),
            Convert.ToHexString(request.Image.Id.Span));

        await collection!.SetAsync(imageGuid,
            imageInfo,
            DateTime.UtcNow,
            propagateToNodes: 2,
            traceId: request.TraceId,
            cancellationToken: context.CancellationToken);
        _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(CreateImage));
        return new Image(request.Image) { Guid = imageGuid.ToString() };
    }

    /// <inheritdoc />
    public override async Task<Image> CreateImagePR(CreateImageMessage request, ServerCallContext context)
    {
        var traceId = string.IsNullOrWhiteSpace(request.TraceId) ? ShortGuid.NewGuid().ToString() : request.TraceId;
        // _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", traceId, nameof(CreateImagePR));

        var imageGuid = string.IsNullOrEmpty(request.Image.Guid)
            ? ImageGuid.New()
            : ImageGuid.Parse(request.Image.Guid);
        var imageInfo = new ImageInfo(new Uri(request.Image.DownloadUri),
            Convert.ToHexString(request.Image.Id.Span));

        var result = await _context.PartiallyReplicatedImageCollectionsRegistry
            .ApplyAsync<AddValueOperation<ImageGuid, ImageInfo, DateTime>, ValueResponse<ImageInfo>>(
                CollectionId.Parse(request.Image.CollectionId),
                new AddValueOperation<ImageGuid, ImageInfo, DateTime>(imageGuid, imageInfo, DateTime.UtcNow,
                    request.PropagateToNodes),
                traceId: traceId,
                cancellationToken: context.CancellationToken);

        if (!result.Success)
        {
            _logger.LogError("TraceId {TraceId}: {FuncName} failed with message \"{Message}\"",
                traceId, nameof(CreateImagePR), result.Message);
        }

        // else _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", traceId, nameof(CreateImagePR));
        return result.Value != default
            ? new Image(request.Image) { Guid = imageGuid.ToString() }
            : new Image();
    }

    /// <inheritdoc />
    public override Task<Image> GetImage(ImageUuids request, ServerCallContext context)
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
        return Task.FromResult(new Image
        {
            Guid = request.ImageUuid,
            CollectionId = request.CollectionId,
            Id = ByteString.CopyFrom(Convert.FromHexString(image!.ImageId)),
            DownloadUri = image.DownloadUrl.ToString()
        });
    }

    /// <inheritdoc />
    public override async Task<Image> GetImagePR(ImageUuids request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetImagePR));
        var result = await _context.PartiallyReplicatedImageCollectionsRegistry
            .ApplyAsync<GetValueOperation<ImageGuid>, ValueResponse<ImageInfo>>(
                CollectionId.Parse(request.CollectionId),
                new GetValueOperation<ImageGuid>(ImageGuid.Parse(request.ImageUuid)),
                traceId: request.TraceId);

        _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", request.TraceId, nameof(GetImagePR));
        return new Image
        {
            Guid = request.ImageUuid,
            CollectionId = request.CollectionId,
            Id = ByteString.CopyFrom(Convert.FromHexString(result.Value?.Value?.ImageId ?? "")),
            DownloadUri = result.Value?.Value?.DownloadUrl.ToString()
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
            ImageUuid = { result.Value?.Value.Select(i => i.ToString()) }
        };
    }

    /// <inheritdoc />
    public override async Task<BoolResponse> DeleteImagePR(DeleteImageRequest request, ServerCallContext context)
    {
        var traceId = string.IsNullOrWhiteSpace(request.TraceId) ? ShortGuid.NewGuid().ToString() : request.TraceId;
        // _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", traceId, nameof(DeleteImagePR));

        var result = await _context.PartiallyReplicatedImageCollectionsRegistry
            .ApplyAsync<DeleteImageOperation, ValueResponse<bool>>(
                CollectionId.Parse(request.CollectionId),
                new DeleteImageOperation(ImageGuid.Parse(request.ImageUuid), DateTime.UtcNow,
                    request.PropagateToNodes),
                traceId: traceId);

        if (!result.Success)
        {
            _logger.LogError("TraceId {TraceId}: {FuncName} failed with message \"{Message}\"",
                traceId, nameof(CreateImagePR), result.Message);
        }

        // else _logger.LogDebug("TraceId {TraceId}: {FuncName} finished", traceId, nameof(DeleteImagePR));
        return new BoolResponse { Value = result.Success };
    }

    /// <inheritdoc />
    public override async Task<BoolResponse> DeleteCollection(CollectionIdMessage request,
        ServerCallContext context)
    {
        await _context.ImageCollectionsRegistry.RemoveAsync(CollectionId.Parse(request.Id),
            cancellationToken: context.CancellationToken);
        return new BoolResponse { Value = true };
    }

    /// <inheritdoc />
    public override async Task<BoolResponse> DeleteCollectionPR(CollectionIdMessage request,
        ServerCallContext context)
    {
        return new BoolResponse
        {
            Value = await _context.PartiallyReplicatedImageCollectionsRegistry.TryRemoveCollectionAsync(
                CollectionId.Parse(request.Id),
                cancellationToken: context.CancellationToken)
        };
    }

    /// <inheritdoc />
    public override async Task<BoolResponse> ImagesCollectionExists(CollectionIdMessage request,
        ServerCallContext context) =>
        new()
        {
            Value = _context.ImageCollectionsRegistry.TryGetValue(CollectionId.Parse(request.Id), out _)
        };

    /// <inheritdoc />
    public override async Task<BoolResponse> ImagesCollectionExistsPR(CollectionIdMessage request,
        ServerCallContext context) =>
        new()
        {
            Value = _context.PartiallyReplicatedImageCollectionsRegistry
                .CollectionExists(CollectionId.Parse(request.Id))
        };

    private static void ThrowNotFound(string details) =>
        throw new RpcException(new Status(StatusCode.NotFound, details));

    public override async Task<UserResponse> CreateUser(UserCreateRequest request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(CreateUser));

        var id = Guid.NewGuid();
        var user = new User(id, request.FirstName, request.LastName);
        await _context.UserObservedRemoveSet.AddAsync(user);

        _logger.LogDebug("TraceId {TraceId}: {FuncName} ending", request.TraceId, nameof(CreateUser));

        return new UserResponse
        {
            Guid = id.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            TraceId = request.TraceId
        };
    }

    public override Task<UserResponse> GetUser(UserGetRequest request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetUser));

        var (id, firstName, lastName) =
            _context.UserObservedRemoveSet.Value.First(u => u.Id.ToString() == request.Id);

        _logger.LogDebug("TraceId {TraceId}: {FuncName} ending", request.TraceId, nameof(GetUser));

        return Task.FromResult(new UserResponse
        {
            Guid = id.ToString(),
            FirstName = firstName,
            LastName = lastName,
            TraceId = request.TraceId
        });
    }

    public override Task<UsersResponse> GetAllUsers(UsersGetRequest request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(GetAllUsers));

        var results = _context.UserObservedRemoveSet.Value.Select(u => new UserResponse
        {
            Guid = u.Id.ToString(),
            FirstName = u.FirstName,
            LastName = u.LastName,
            TraceId = request.TraceId
        });

        var response = new UsersResponse();
        response.Users.AddRange(results);

        _logger.LogDebug("TraceId {TraceId}: {FuncName} ending", request.TraceId, nameof(GetAllUsers));

        return Task.FromResult(response);
    }

    public override async Task<UserDeleteResponse> DeleteUser(UserDeleteRequest request, ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(DeleteUser));

        await _context.UserObservedRemoveSet.RemoveAsync(u => u.Id.ToString() == request.Id);

        _logger.LogDebug("TraceId {TraceId}: {FuncName} ending", request.TraceId, nameof(DeleteUser));

        return new UserDeleteResponse
        {
            Id = request.Id,
            TraceId = request.TraceId
        };
    }

    public override async Task<UsersDeleteResponse> DeleteAllUsers(UsersDeleteRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("TraceId {TraceId}: {FuncName} starting", request.TraceId, nameof(DeleteAllUsers));

        await _context.UserObservedRemoveSet.RemoveAsync(_ => true);

        _logger.LogDebug("TraceId {TraceId}: {FuncName} ending", request.TraceId, nameof(DeleteAllUsers));

        return new UsersDeleteResponse();
    }
}
