using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Grpc;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf.Grpc;

namespace Nyris.Crdt.Tests;

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
            traceId: _context.Nodes.InstanceId.ToString(),
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