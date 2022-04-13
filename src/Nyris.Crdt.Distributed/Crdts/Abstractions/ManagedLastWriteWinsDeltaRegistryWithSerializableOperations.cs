using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract class ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<TKey, TValue, TTimeStamp>
        : ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>,
            IAcceptOperations<RegistryOperation, RegistryOperationResponse>
        where TValue : IHashable
        where TKey : IEquatable<TKey>, IComparable<TKey>, IHashable
        where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
    {
        /// <inheritdoc />
        protected ManagedLastWriteWinsDeltaRegistryWithSerializableOperations(InstanceId id,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
        }

        /// <inheritdoc />
        public virtual async Task<RegistryOperationResponse> ApplyAsync(RegistryOperation operation,
            CancellationToken cancellationToken = default)
        {
            switch (operation)
            {
                case GetValueOperation<TKey> getValueOperation:
                    if (!TryGetValue(getValueOperation.Key, out var value))
                    {
                        throw new KeyNotFoundException($"Key {getValueOperation.Key} was not found in registry");
                    }
                    return new ValueResponse<TValue>(value);
                case AddValueOperation<TKey, TValue, TTimeStamp>(var key, var newValue, var timeStamp, var propagateToNodes):
                    var added = await SetAsync(key, newValue, timeStamp,
                        propagateToNodes,
                        cancellationToken: cancellationToken);
                    return new ValueResponse<TValue>(added.Value);
                default:
                    throw new GeneratedCodeExpectationsViolatedException(
                        $"Apply async received operation of unexpected type: {operation.GetType()}");
            }
        }
    }
}