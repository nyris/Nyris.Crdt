using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers
{
	public class MockManagedCrdtRegistryBase : ManagedCrdtRegistryBase<InstanceId, MockUserCRDT, MockUserDto>
	{
		public MockManagedCrdtRegistryBase(InstanceId instanceId, IAsyncQueueProvider? queueProvider, ILogger? logger = null) : base(instanceId, queueProvider, logger)
		{
		}

		public override Task<MergeResult> MergeAsync(MockUserDto other, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public override Task<MockUserDto> ToDtoAsync(CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public override IAsyncEnumerable<MockUserDto> EnumerateDtoBatchesAsync(CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public override ReadOnlySpan<byte> CalculateHash()
		{
			throw new NotImplementedException();
		}

		public override ulong Size { get; }
        public override ulong StorageSize { get; }

        public override async IAsyncEnumerable<KeyValuePair<InstanceId, MockUserCRDT>> EnumerateItems(
			CancellationToken cancellationToken = default
		)
		{
		    yield break;
		}
	}
}