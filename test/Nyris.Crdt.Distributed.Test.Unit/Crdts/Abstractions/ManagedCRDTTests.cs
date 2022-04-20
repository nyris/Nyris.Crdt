using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Test.Unit.Helpers;
using Xunit;

namespace Nyris.Crdt.Distributed.Test.Unit.Crdts.Abstractions;

public class ManagedCRDTTests
{
    private readonly MockUserCRDT _managedCrdt;

    public ManagedCRDTTests()
    {
        _managedCrdt = new MockUserCRDT(new InstanceId("mock-user-crdt-id"), null, null);
    }

    [Fact]
    public void Gets_TypeName()
    {
         _managedCrdt.GetType().Should().Be(typeof(MockUserCRDT));
    }

    [Fact]
    public async Task StateChangedAsync_Trigger_ToDtoAsync()
    {
        await _managedCrdt.StateChangedAsync();

		_managedCrdt.ToDtoAsyncCalls.Should().Be(1);
	}
}