﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Services;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Extensions.Guids;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    /// <summary>
    /// Base type for all managedCRDTs. When inheriting, do not forget to call <see cref="StateChangedAsync"/> in all update methods.
    /// </summary>
    /// <remarks>
    ///     DO NOT CHANGE THIS TYPE'S NAME LIGHTLY
    ///     It is used without referencing the type directly (or project at all) in the SourceGenerator project as const string.
    /// </remarks>
    /// <typeparam name="TDto">It's a dto type, that is used as a data contract for grpc
    /// communication. So it should be properly annotated with <see cref="ProtoContract"/>
    /// and <see cref="ProtoMember"/>.</typeparam>
    public abstract class ManagedCRDT<TDto>
        : IAsyncCRDT<TDto>, IAsyncDtoBatchProvider<TDto>, IHashable
    {
        public readonly string InstanceId;
        private readonly AsyncQueue<DtoMessage<TDto>> _queue;
        private string? _typeName;
        private readonly ConcurrentBag<IReactToOtherCrdtChange> _dependentCrdts = new();
        private readonly ILogger? _logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="instanceId">Instance id is used to match instances of the same type across nodes.
        /// For example, if there nodes (servers) A and B and they try to share two instanced of CRDT type T.
        /// When node A updates first instance and sends that update to B, B needs to somehow
        /// distinguish which instance was updated. </param>
        /// <param name="logger"></param>
        protected ManagedCRDT(string instanceId, ILogger? logger = null)
        {
            _queue = Queues.GetQueue<TDto>(GetType());
            InstanceId = instanceId;
            _logger = logger;
        }

        internal string TypeName
        {
            get
            {
                if (_typeName != null) return _typeName;
                _typeName = TypeNameCompressor.GetName(GetType());
                return _typeName;
            }
        }

        internal void AddDependent(IReactToOtherCrdtChange crdt) => _dependentCrdts.Add(crdt);

        /// <inheritdoc />
        public abstract Task<MergeResult> MergeAsync(TDto other, CancellationToken cancellationToken = default);

        /// <param name="cancellationToken"></param>
        /// <inheritdoc />
        public abstract Task<TDto> ToDtoAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default);

        protected internal async Task StateChangedAsync(int propagationCounter = 0,
            string? traceId = null,
            CancellationToken cancellationToken = default)
        {
            var dtoMessage = new DtoMessage<TDto>(TypeName,
                InstanceId,
                await ToDtoAsync(cancellationToken),
                traceId ?? ShortGuid.Encode(Guid.NewGuid()),
                propagationCounter);

            _logger?.LogDebug("TraceId: {TraceId}, enqueueing dto after state was changed", traceId);
            await _queue.EnqueueAsync(dtoMessage, cancellationToken);

            if (_dependentCrdts.IsEmpty)
            {
                await dtoMessage.MaybeWaitForCompletionAsync(cancellationToken);
                _logger?.LogDebug("TraceId: {TraceId}, dto was sent and awaited, returning", traceId);
                return;
            }

            await Task.WhenAll(_dependentCrdts
                .Select(crdt => crdt.HandleChangeInAnotherCrdtAsync(InstanceId, cancellationToken))
                .Append(dtoMessage.MaybeWaitForCompletionAsync(cancellationToken)));
            _logger?.LogDebug("TraceId: {TraceId}, dto was sent and awaited (including dependent crdts), returning", traceId);
        }

        /// <inheritdoc />
        public abstract ReadOnlySpan<byte> CalculateHash();
    }
}