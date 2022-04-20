using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers;

public class MockUserCRDT : ManagedCRDT<MockUserDto>
{
    public int ToDtoAsyncCalls { get; set; }

    public MockUserCRDT(InstanceId instanceId, IAsyncQueueProvider? queueProvider, ILogger? logger = null) : base(instanceId, queueProvider, logger)
    {
    }

    public override Task<MergeResult> MergeAsync(MockUserDto other, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task<MockUserDto> ToDtoAsync(CancellationToken cancellationToken = default)
    {
        ToDtoAsyncCalls++;

        return Task.FromResult(new MockUserDto());
    }

    public override IAsyncEnumerable<MockUserDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override ReadOnlySpan<byte> CalculateHash()
    {
        throw new NotImplementedException();
    }
}