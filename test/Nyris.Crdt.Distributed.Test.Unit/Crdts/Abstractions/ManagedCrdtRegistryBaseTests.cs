using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Test.Unit.Helpers;
using Xunit;

namespace Nyris.Crdt.Distributed.Test.Unit.Crdts.Abstractions;

public class ManagedCrdtRegistryBaseTests : MockManagedCrdtRegistryBase
{
	private readonly Mock<IIndex<InstanceId, MockUserCRDT>> _indexMock;

	public ManagedCrdtRegistryBaseTests() : base(new InstanceId("mock-instance-id"), null, null)
	{
		_indexMock = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		_indexMock.SetupGet(x => x.UniqueName).Returns("mock-instance-id");
	}

	[Fact]
	public void TryGetIndex_returns_False_OnEmpty()
	{
		TryGetIndex("mock-absent-index", out IIndex<InstanceId, MockUserCRDT>? _).Should().BeFalse();
	}

	[Fact]
	public async Task TryGetIndex_Gets_Added_Index()
	{
		var index = _indexMock.Object;

		await AddIndexAsync(index);

		TryGetIndex(index.UniqueName, out IIndex<InstanceId, MockUserCRDT>? result);

		index.Should().BeEquivalentTo(result);
	}

	[Fact]
	public async Task Can_RemoveIndex()
	{
		var index = _indexMock.Object;

		await AddIndexAsync(index);

		RemoveIndex(index);

		TryGetIndex(index.UniqueName, out IIndex<InstanceId, MockUserCRDT>? result);

		result.Should().BeNull();
	}

	[Fact]
	public async Task Can_AddItemToIndexesAsync()
	{
		var crdt = new MockUserCRDT(new InstanceId("mock-user-crdt"), null,null);

		var indexMock1 = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		indexMock1.SetupGet(x => x.UniqueName).Returns("mock-1");
		var index1 = indexMock1.Object;

		var indexMock2 = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		indexMock2.SetupGet(x => x.UniqueName).Returns("mock-2");
		var index2 = indexMock2.Object;

		var indexMock3 = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		indexMock3.SetupGet(x => x.UniqueName).Returns("mock-3");
		var index3 = indexMock3.Object;

		await AddIndexAsync(index1);
		await AddIndexAsync(index2);
		await AddIndexAsync(index3);

		await AddItemToIndexesAsync(crdt.InstanceId, crdt);

		indexMock1.Verify(x => x.AddAsync(It.IsAny<InstanceId>(), It.IsAny<MockUserCRDT>(), It.IsAny<CancellationToken>()), Times.Once);
		indexMock2.Verify(x => x.AddAsync(It.IsAny<InstanceId>(), It.IsAny<MockUserCRDT>(), It.IsAny<CancellationToken>()), Times.Once);
		indexMock3.Verify(x => x.AddAsync(It.IsAny<InstanceId>(), It.IsAny<MockUserCRDT>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Can_RemoveFromIndexes()
	{
		var crdt = new MockUserCRDT(new InstanceId("mock-user-crdt"), null,null);

		var indexMock1 = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		indexMock1.SetupGet(x => x.UniqueName).Returns("mock-1");
		var index1 = indexMock1.Object;

		var indexMock2 = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		indexMock2.SetupGet(x => x.UniqueName).Returns("mock-2");
		var index2 = indexMock2.Object;

		var indexMock3 = new Mock<IIndex<InstanceId, MockUserCRDT>>();
		indexMock3.SetupGet(x => x.UniqueName).Returns("mock-3");
		var index3 = indexMock3.Object;

		await AddIndexAsync(index1);
		await AddIndexAsync(index2);
		await AddIndexAsync(index3);

		await RemoveItemFromIndexes(crdt.InstanceId, crdt);

		indexMock1.Verify(x => x.RemoveAsync(It.IsAny<InstanceId>(), It.IsAny<MockUserCRDT>(), It.IsAny<CancellationToken>()), Times.Once);
		indexMock2.Verify(x => x.RemoveAsync(It.IsAny<InstanceId>(), It.IsAny<MockUserCRDT>(), It.IsAny<CancellationToken>()), Times.Once);
		indexMock3.Verify(x => x.RemoveAsync(It.IsAny<InstanceId>(), It.IsAny<MockUserCRDT>(), It.IsAny<CancellationToken>()), Times.Once);
	}
}