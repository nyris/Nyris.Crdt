using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Extensions.Guids;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Metrics;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.Distributed
{
    public abstract class ManagedCrdtContext
    {
        private readonly ConcurrentDictionary<InstanceId, IHashable> _managedCrdts = new();
        // private readonly ConcurrentDictionary<TypeNameAndInstanceId, IHashable> _sameManagedCrdts = new();
        // private readonly ConcurrentDictionary<Type, HashSet<TypeNameAndInstanceId>> _typeToTypeNameMapping = new();

        private readonly ConcurrentDictionary<InstanceId, INodesWithReplicaProvider> _partiallyReplicated = new();
        private readonly ConcurrentDictionary<InstanceId, ICreateAndDeleteManagedCrdtsInside> _holders = new();

        protected readonly ILogger<ManagedCrdtContext>? Logger;

        public NodeSet Nodes { get; }

        protected ManagedCrdtContext(
            NodeInfo nodeInfo,
            ILogger<ManagedCrdtContext>? logger = null,
            NodeSet? nodes = null,
            ICrdtMetricsRegistry? metricsRegistry = null
        )
        {
            Logger = logger;
            Nodes = nodes ?? new NodeSet(new InstanceId("nodes_internal"), nodeInfo, metricsRegistry: metricsRegistry);
            Add<NodeSet, NodeSet.NodeSetDto>(Nodes);
        }

        protected internal void Add<TCrdt, TDto>(TCrdt crdt)
            where TCrdt : ManagedCRDT<TDto>
        {
            if (typeof(TCrdt).IsGenericType)
            {
                throw new ManagedCrdtContextSetupException("Only non generic types can be used in a managed " +
                                                           "context (because library needs to generate concrete grpc " +
                                                           "messaged based on dto type used)." +
                                                           $"Try creating concrete type, that inherits from {typeof(TCrdt)}");
            }

            if (!_managedCrdts.TryAdd(crdt.InstanceId, crdt))
            {
                throw new ManagedCrdtContextSetupException($"Failed to add crdt of type {typeof(TCrdt)} " +
                                                           $"with id {crdt.InstanceId} to the context - crdt of that" +
                                                           $" type and with that instanceId ({crdt.InstanceId}) was " +
                                                           "already added. Make sure instanceId " +
                                                           "is unique withing crdts of the same type.");
            }

            if (crdt is ICreateAndDeleteManagedCrdtsInside compoundCrdt)
            {
                compoundCrdt.ManagedCrdtContext = this;
            }

            if (crdt is IReactToOtherCrdtChange crdtWithDependencies)
            {
                Nodes.AddDependent(crdtWithDependencies);
            }
        }

        internal void Add<TCrdt, TDto>(
            TCrdt crdt,
            INodesWithReplicaProvider nodesWithReplicaProvider,
            ICreateAndDeleteManagedCrdtsInside holder
        )
            where TCrdt : ManagedCRDT<TDto>
        {
            Add<TCrdt, TDto>(crdt);
            _partiallyReplicated.TryAdd(crdt.InstanceId, nodesWithReplicaProvider);
            _holders.TryAdd(crdt.InstanceId, holder);
        }

        internal void Remove<TCrdt, TDto>(TCrdt crdt)
            where TCrdt : ManagedCRDT<TDto>
        {
            // Logger?.LogDebug("Instance {InstanceId} of crdt {CrdtName} is being removed from context",
            //     crdt.InstanceId, crdt.TypeName);
            _managedCrdts.TryRemove(crdt.InstanceId, out _);
        }

        internal async Task RemoveLocallyAsync<TCrdt, TDto>(
            InstanceId instanceId,
            CancellationToken cancellationToken = default
        )
            where TCrdt : ManagedCRDT<TDto>
        {
            if (_holders.TryGetValue(instanceId, out var holder))
            {
                await holder.MarkForDeletionLocallyAsync(instanceId, cancellationToken);
            }

            _partiallyReplicated.TryRemove(instanceId, out _);
            if (TryGetCrdt<TCrdt, TDto>(instanceId, out var crdt))
            {
                Remove<TCrdt, TDto>(crdt);
            }
        }

        public async Task<TDto> MergeAsync<TCrdt, TDto>(
            TDto dto,
            InstanceId instanceId,
            uint propagateToNodes = 0,
            bool allowPropagation = true,
            string? traceId = null,
            CancellationToken cancellationToken = default
        )
            where TCrdt : ManagedCRDT<TDto>
        {
            if (!TryGetCrdt<TCrdt, TDto>(instanceId, out var crdt))
            {
                throw new ManagedCrdtContextSetupException(
                                                           $"TraceId {traceId}: Merging dto of type {typeof(TDto)} with id {instanceId} " +
                                                           "failed. Check that you Add-ed appropriate managed crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            var mergeResult = await crdt.MergeAsync(dto, cancellationToken);
            // Logger?.LogDebug("TraceId {TraceId}: merging result {MergeResult} for {CrdtName} ({InstanceId})",
            // traceId, mergeResult, TypeNameCompressor.GetName<TCrdt>(), instanceId);
            if (allowPropagation && mergeResult == MergeResult.ConflictSolved)
            {
                await crdt.StateChangedAsync(propagateToNodes: propagateToNodes,
                                             fromMerge: true,
                                             traceId: traceId,
                                             cancellationToken: cancellationToken);
            }

            return await crdt.ToDtoAsync(cancellationToken);
        }

        public Task<Response<TCollectionOperationResponse>> ApplyAsync<TCrdt,
                                                                       TKey,
                                                                       TCollection,
                                                                       TCollectionKey,
                                                                       TCollectionValue,
                                                                       TCollectionDto,
                                                                       TCollectionOperationBase,
                                                                       TCollectionOperationResponseBase,
                                                                       TCollectionFactory,
                                                                       TCollectionOperation,
                                                                       TCollectionOperationResponse
        >(
            TKey key,
            TCollectionOperation operation,
            InstanceId instanceId,
            string? traceId = null,
            CancellationToken cancellationToken = default
        )
            where TCrdt : PartiallyReplicatedCRDTRegistry<TKey,
                TCollection,
                TCollectionKey,
                TCollectionValue,
                TCollectionDto,
                TCollectionOperationBase,
                TCollectionOperationResponseBase,
                TCollectionFactory>
            where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
            where TCollection : ManagedCrdtRegistryBase<TCollectionKey, TCollectionValue, TCollectionDto>,
            IAcceptOperations<TCollectionOperationBase, TCollectionOperationResponseBase>
            where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionDto>, new()
            where TCollectionOperationBase : Operation, ISelectShards
            where TCollectionOperationResponseBase : OperationResponse
            where TCollectionOperation : TCollectionOperationBase
            where TCollectionOperationResponse : TCollectionOperationResponseBase
            where TCollectionKey : notnull
        {
            if (!TryGetCrdt<TCrdt,
                    PartiallyReplicatedCRDTRegistry<TKey,
                        TCollection,
                        TCollectionKey,
                        TCollectionValue,
                        TCollectionDto,
                        TCollectionOperationBase,
                        TCollectionOperationResponseBase,
                        TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>(instanceId, out var crdt))
            {
                throw new ManagedCrdtContextSetupException(
                                                           $"Applying operation of type {typeof(TCollectionOperationBase)} " +
                                                           $"to crdt {typeof(TCrdt)} with id {instanceId} failed. " +
                                                           "Check that you Add-ed appropriate partially replicated crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            return crdt.ApplyAsync<TCollectionOperation, TCollectionOperationResponse>(key,
                                                                                       operation,
                                                                                       traceId: traceId,
                                                                                       cancellationToken: cancellationToken);
        }

        public Task<Response<TCollectionOperationResponse>> ApplyAsync<TCrdt,
                                                                       TKey,
                                                                       TCollection,
                                                                       TCollectionKey,
                                                                       TCollectionValue,
                                                                       TCollectionDto,
                                                                       TCollectionOperationBase,
                                                                       TCollectionOperationResponseBase,
                                                                       TCollectionFactory,
                                                                       TCollectionOperation,
                                                                       TCollectionOperationResponse
        >(
            ShardId shardId,
            TCollectionOperation operation,
            InstanceId instanceId,
            string? traceId = null,
            CancellationToken cancellationToken = default
        )
            where TCrdt : PartiallyReplicatedCRDTRegistry<TKey,
                TCollection,
                TCollectionKey,
                TCollectionValue,
                TCollectionDto,
                TCollectionOperationBase,
                TCollectionOperationResponseBase,
                TCollectionFactory>
            where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
            where TCollection : ManagedCrdtRegistryBase<TCollectionKey, TCollectionValue, TCollectionDto>,
            IAcceptOperations<TCollectionOperationBase, TCollectionOperationResponseBase>
            where TCollectionFactory : IManagedCRDTFactory<TCollection, TCollectionDto>, new()
            where TCollectionOperationBase : Operation, ISelectShards
            where TCollectionOperationResponseBase : OperationResponse
            where TCollectionOperation : TCollectionOperationBase
            where TCollectionOperationResponse : TCollectionOperationResponseBase
            where TCollectionKey : notnull
        {
            if (!TryGetCrdt<TCrdt,
                    PartiallyReplicatedCRDTRegistry<TKey,
                        TCollection,
                        TCollectionKey,
                        TCollectionValue,
                        TCollectionDto,
                        TCollectionOperationBase,
                        TCollectionOperationResponseBase,
                        TCollectionFactory>.PartiallyReplicatedCrdtRegistryDto>(instanceId, out var crdt))
            {
                throw new ManagedCrdtContextSetupException(
                                                           $"Applying operation of type {typeof(TCollectionOperationBase)} " +
                                                           $"to crdt {typeof(TCrdt)} with id {instanceId} failed. " +
                                                           "Check that you Add-ed appropriate partially replicated crdt type and " +
                                                           "that instanceId of that type is coordinated across servers");
            }

            return crdt.ApplyToSingleShardAsync<TCollectionOperation, TCollectionOperationResponse>(shardId,
                                                                                                    operation,
                                                                                                    traceId: traceId ??
                                                                                                             ShortGuid
                                                                                                                 .Encode(Guid.NewGuid()),
                                                                                                    cancellationToken: cancellationToken);
        }

        public ReadOnlySpan<byte> GetHash(InstanceId instanceId) =>
            _managedCrdts.TryGetValue(instanceId, out var crdt)
                ? crdt.CalculateHash()
                : ArraySegment<byte>.Empty;

        internal IEnumerable<InstanceId> GetInstanceIds<TCrdt>()
            => _managedCrdts
               .Where(pair => pair.Value.GetType() == typeof(TCrdt))
               .Select(pair => pair.Key);

        internal bool IsHashEqual(InstanceId instanceId, ReadOnlySpan<byte> hash)
        {
            if (!_managedCrdts.TryGetValue(instanceId, out var crdt))
            {
                throw new ManagedCrdtContextSetupException(
                                                           "Checking hash for Crdt with id " +
                                                           $"{instanceId} failed - Crdt not found in the managed context. " +
                                                           "Check that you Add-ed appropriate managed crdt type and that instanceId " +
                                                           "of that type is coordinated across servers");
            }

            return crdt.CalculateHash().SequenceEqual(hash);
        }

        public async IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync<TCrdt, TDto>(
            InstanceId instanceId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
            where TCrdt : ManagedCRDT<TDto>
        {
            if (!_managedCrdts.TryGetValue(instanceId, out var crdtObject))
            {
                throw new ManagedCrdtContextSetupException($"Enumerating dto of crdt type {typeof(TCrdt)} " +
                                                           $"with id {instanceId} failed due to crdt not being found." +
                                                           " Did you forget to add that Crdt in ManagedContext?");
            }

            if (crdtObject is not IAsyncDtoBatchProvider<TDto> batchProvider)
            {
                throw new ManagedCrdtContextSetupException(
                                                           $"Internal error: Crdt of type {crdtObject.GetType()} does " +
                                                           "not implement IAsyncDtoBatchProvider. This should not happen.");
            }

            await foreach (var dto in batchProvider.EnumerateDtoBatchesAsync(cancellationToken))
            {
                yield return dto;
            }
        }

        internal IEnumerable<NodeInfo> GetNodesThatHaveReplica(InstanceId instanceId)
        {
            if (_partiallyReplicated.TryGetValue(instanceId, out var nodesWithReplicasProvider))
            {
                return nodesWithReplicasProvider.GetNodesThatShouldHaveReplicaOfCollection(instanceId);
            }

            return Nodes.Value;
        }

        private bool TryGetCrdt<TCrdt, TDto>(
            InstanceId instanceId,
            [NotNullWhen(true)] out TCrdt? crdt
        )
            where TCrdt : ManagedCRDT<TDto>
        {
            _managedCrdts.TryGetValue(instanceId, out var crdtObject);
            crdt = crdtObject as TCrdt;
            return crdt != null;
        }
    }
}
