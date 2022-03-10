using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf.Grpc;

namespace Nyris.Crdt.Tests;

public class TestOperationPassingGrpcService<TCrdt,
											 TKey,
											 TCollection,
											 TCollectionKey,
											 TCollectionValue,
											 TCollectionDto,
											 TCollectionOperationBase,
											 TCollectionOperationResponseBase,
											 TCollectionFactory,
											 TOperation,
											 TResponse> : IOperationPassingGrpcService<TOperation, TResponse>
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
	where TOperation : TCollectionOperationBase
	where TResponse : TCollectionOperationResponseBase
{
	private readonly ManagedCrdtContext _context;

	public TestOperationPassingGrpcService(ManagedCrdtContext context)
	{
		_context = context;
	}

	/// <inheritdoc />
	public Task<TResponse> ApplyAsync(CrdtOperation<TOperation> operation, CancellationToken cancellationToken = default)
	{
		return _context.ApplyAsync<TCrdt,
			TKey,
			TCollection,
			TCollectionKey,
			TCollectionValue,
			TCollectionDto,
			TCollectionOperationBase,
			TCollectionOperationResponseBase,
			TCollectionFactory,
			TOperation,
			TResponse>(operation.ShardId,
					   operation.Operation,
					   operation.InstanceId,
					   operation.TraceId,
					   operation.PropagateToNodes,
					   cancellationToken);
	}
}

public class TestDtoPassingGrpcService<TCRDT, TDto> : IDtoPassingGrpcService<TDto>
    where TCRDT : ManagedCRDT<TDto>
{
    private readonly ManagedCrdtContext _context;

    public TestDtoPassingGrpcService(ManagedCrdtContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<TDto> SendAsync(DtoMessage<TDto> msg, CancellationToken cancellationToken = default) =>
        _context.MergeAsync<TCRDT, TDto>(msg.Value, msg.InstanceId,
            propagationCounter: msg.PropagationCounter > 0 ? msg.PropagationCounter - 1 : 0,
            traceId: _context.Nodes.InstanceId,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task<byte[]> GetHashAsync(TypeNameAndInstanceId nameAndInstanceId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> EnumerateCrdtAsync(IAsyncEnumerable<TDto> dtos, CallContext context = default)
    {
        throw new NotImplementedException();
    }
}