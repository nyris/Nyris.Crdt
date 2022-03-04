using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageInfoLwwCollectionWithSerializableOperations
        : ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<ImageGuid,
            ImageInfo,
            DateTime>
    {
        /// <inheritdoc />
        public ImageInfoLwwCollectionWithSerializableOperations(string instanceId, ILogger? logger = null)
            : base(instanceId, logger)
        {
        }

        /// <inheritdoc />
        public override async Task<RegistryOperationResponse> ApplyAsync(RegistryOperation operation, CancellationToken cancellationToken = default)
        {
            return operation switch
            {
                FindIdsOperation findIdsOperation => new ValueResponse<IList<ImageGuid>>(await FindAsync(findIdsOperation.ImageId)),
                _ => await base.ApplyAsync(operation, cancellationToken)
            };
        }

        public async Task<IList<ImageGuid>> FindAsync(string imageId)
        {
            return TryGetIndex<ImageIdIndex>(ImageIdIndex.IndexName, out var index)
                ? await index.FindAsync(imageId)
                : ArraySegment<ImageGuid>.Empty;
        }

        public sealed class ImageInfoLwwCollectionWithSerializableOperationsFactory
            : IManagedCRDTFactory<ImageInfoLwwCollectionWithSerializableOperations, LastWriteWinsDto>
        {
            private readonly ILogger? _logger;

            public ImageInfoLwwCollectionWithSerializableOperationsFactory()
            {
            }

            public ImageInfoLwwCollectionWithSerializableOperationsFactory(ILogger? logger)
            {
                _logger = logger;
                _logger?.LogDebug("{FactoryName} created with logger",
                    nameof(ImageInfoLwwCollectionWithSerializableOperationsFactory));
            }

            /// <inheritdoc />
            public ImageInfoLwwCollectionWithSerializableOperations Create(string instanceId)
                => new (instanceId, _logger);
        }
    }
}