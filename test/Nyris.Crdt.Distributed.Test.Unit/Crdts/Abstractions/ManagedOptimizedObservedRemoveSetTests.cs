using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Test.Unit.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Nyris.Crdt.Distributed.Test.Unit.Crdts.Abstractions;

public class ManagedOptimizedObservedRemoveSetTests
{
    private readonly ITestOutputHelper _testOutput;
    private readonly MockManagedOptimizedObservedRemoveSet _managedCrdt;
    private readonly MockOldMergeManagedOptimizedObservedRemoveSet _managedCrdtOldMerge;
    private static int _userCount;

    public ManagedOptimizedObservedRemoveSetTests(ITestOutputHelper testOutput)
    {
        _userCount = 0;

        _testOutput = testOutput;
        var managedCrdtMock =
            new Mock<MockManagedOptimizedObservedRemoveSet>(new InstanceId("mock-user-or-set-crdt-id"),
                    NodeId.GenerateNew(),
                    It.IsAny<IQueryProvider>(), It.IsAny<ILogger>())
                { CallBase = true };
        _managedCrdt = managedCrdtMock.Object;

        managedCrdtMock.Setup(x => x.StateChangedAsync(
            It.IsAny<uint>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        )).Returns(Task.CompletedTask);

        var managedCrdtOldMergeMock =
            new Mock<MockOldMergeManagedOptimizedObservedRemoveSet>(
                    new InstanceId("mock-old-merge-user-or-set-crdt-id"), It.IsAny<IQueryProvider>(),
                    It.IsAny<ILogger>())
                { CallBase = true };
        _managedCrdtOldMerge = managedCrdtOldMergeMock.Object;

        managedCrdtOldMergeMock.Setup(x => x.StateChangedAsync(
            It.IsAny<uint>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>())
        ).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Can_Merge_Different_Dtos()
    {
        var mockNodeA = new NodeId("mock-or-set-node-a");
        var mockNodeB = new NodeId("mock-or-set-node-b");
        var mockNodeC = new NodeId("mock-or-set-node-c");

        var dtoB = GetRandomDto(mockNodeB, 3);
        var dtoC = GetRandomDto(mockNodeC, 3);

        var localItems = GetRandomItems(mockNodeA, 4);

        foreach (var item in localItems)
        {
            await _managedCrdt.AddAsync(item.Value);
        }

        var resultB = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);
        var resultC = await _managedCrdt.MergeAsync(dtoC, CancellationToken.None);

        resultB.Should().Be(MergeResult.ConflictSolved);
        resultC.Should().Be(MergeResult.ConflictSolved);

        _managedCrdt.Value.Count.Should().Be(10);
    }

    [Fact]
    public async Task Can_Merge_Same_Dtos()
    {
        var mockNodeA = new NodeId("mock-or-set-node-a");
        var mockNodeB = new NodeId("mock-or-set-node-b");

        var dtoB = GetRandomDto(mockNodeB, 3);

        var localItems = GetRandomItems(mockNodeA, 4);

        foreach (var item in localItems)
        {
            await _managedCrdt.AddAsync(item.Value);
        }

        var resultB0 = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);
        var resultB1 = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);
        var resultB2 = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);

        resultB0.Should().Be(MergeResult.ConflictSolved);
        resultB1.Should().Be(MergeResult.Identical);
        resultB2.Should().Be(MergeResult.Identical);

        _managedCrdt.Value.Should().BeEquivalentTo(dtoB.Items!.Union(localItems).Select(i => i.Value).ToHashSet());
    }

    [Fact]
    public async Task Can_Merge_Deleted_Dtos()
    {
        var mockNodeA = new NodeId("mock-or-set-node-a");
        var mockNodeB = new NodeId("mock-or-set-node-b");

        var dtoB = GetRandomDto(mockNodeB, 3);

        var localItems = GetRandomItems(mockNodeA, 4);

        foreach (var item in localItems)
        {
            await _managedCrdt.AddAsync(item.Value);
        }

        var resultB0 = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);

        // Delete Last Value
        dtoB.VersionVectors![mockNodeB] += 1;
        var deletedItem = dtoB.Items!.Last();

        dtoB.Items!.ExceptWith(new[] { deletedItem });

        dtoB.Tombstones!.Add(deletedItem.Dot, new HashSet<NodeId> { mockNodeB });

        // Add New Value to get ConflictSolved result
        dtoB.VersionVectors![mockNodeB] += 1;
        dtoB.Items.Add(new DottedItem<NodeId, MockUser>(new Dot<NodeId>(mockNodeB, dtoB.VersionVectors![mockNodeB]),
            new MockUser(Guid.NewGuid(), "mock-user-" + _userCount++)));


        var resultB1 = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);

        resultB0.Should().Be(MergeResult.ConflictSolved);
        resultB1.Should().Be(MergeResult.ConflictSolved);

        _managedCrdt.Value.Should().BeEquivalentTo(dtoB.Items!.Union(localItems).Select(i => i.Value).ToHashSet());
    }

    [Fact]
    public async Task New_MergeFunc_Comparable_to_Old_MergeFunc()
    {
        var localNodeA = new NodeId("local-node-a");
        var remoteNodeB = new NodeId("remote-node-b");
        const uint initialLocalItems = 4U;
        // Start off with some items in local state
        var localItems = GetRandomItems(localNodeA, initialLocalItems);

        foreach (var item in localItems)
        {
            await _managedCrdt.AddAsync(item.Value);
            await _managedCrdtOldMerge.AddAsync(item.Value, localNodeA);
        }

        var rand = new Random();
        var iterations = rand.Next(0, 200);
        var totalRemoveOps = 0;
        var dtoBOperations = 0U;
        var remoteNodeBAllItems = new HashSet<DottedItem<NodeId, MockUser>>();

        // Do Random Amount of Operations, to get remote/other items merged in local state
        foreach (var _ in Enumerable.Range(1, iterations).ToList())
        {
            var isRemoveOperation = rand.Next(0, 100) > 50;

            var remoteNodeItems = new HashSet<DottedItem<NodeId, MockUser>>
            {
                new(new Dot<NodeId>(remoteNodeB, ++dtoBOperations),
                    new MockUser(Guid.NewGuid(), "mock-user-" + _userCount++)),
                new(new Dot<NodeId>(remoteNodeB, ++dtoBOperations),
                    new MockUser(Guid.NewGuid(), "mock-user-" + _userCount++)),
                new(new Dot<NodeId>(remoteNodeB, ++dtoBOperations),
                    new MockUser(Guid.NewGuid(), "mock-user-" + _userCount++)),
            };

            // i.e Simulate Whole State of NodeB being sent
            remoteNodeBAllItems.UnionWith(remoteNodeItems);

            var dtoB = new MockManagedOptimizedObservedRemoveSet.MockUserSetDto
            {
                VersionVectors = new Dictionary<NodeId, uint>
                {
                    { remoteNodeB, dtoBOperations }
                },
                Items = remoteNodeBAllItems,
                Tombstones = new Dictionary<Dot<NodeId>, HashSet<NodeId>>()
            };

            var dtoBOld = new MockOldMergeManagedOptimizedObservedRemoveSet.MockUserSetDto
            {
                VersionVectors = dtoB.VersionVectors,
                Items = dtoB.Items
            };

            // Add New Data always, One reason so it can perform Add Operation and Other so It can provide some data to be deleted
            // as you can only remove something that you had previous from other Node (i.e Only Observed things can be Removed OR-Set)
            await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);
            await _managedCrdtOldMerge.MergeAsync(dtoBOld, CancellationToken.None);

            if (isRemoveOperation)
            {
                totalRemoveOps += 1;

                // Delete Last Value 1 Op
                dtoBOperations += 1;
                dtoB.VersionVectors![remoteNodeB] = dtoBOperations;
                var itemToRemove = remoteNodeItems.Last();
                dtoB.Items!.ExceptWith(new[] { itemToRemove });
                dtoB.Tombstones!.Add(itemToRemove.Dot, new HashSet<NodeId> { remoteNodeB });

                dtoBOld = new MockOldMergeManagedOptimizedObservedRemoveSet.MockUserSetDto
                {
                    VersionVectors = dtoB.VersionVectors,
                    Items = dtoB.Items,
                };
            }

            await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);
            await _managedCrdtOldMerge.MergeAsync(dtoBOld, CancellationToken.None);
        }

        _testOutput.WriteLine(
            $"Total iterations: {iterations}, Total Add Ops: {iterations}, Total Remove Ops: {totalRemoveOps}");
        _testOutput.WriteLine(
            $"Total Items in New: {_managedCrdt.Value.Count}, Total Items in Old: {_managedCrdtOldMerge.Value.Count}");

        _managedCrdt.Value.Should().BeEquivalentTo(_managedCrdtOldMerge.Value);
        // NOTE: * 3 is for 3 items added every iteration
        _managedCrdt.Value.Count.Should().Be((iterations * 3) - totalRemoveOps + (int) initialLocalItems);
    }

    [Fact]
    public async Task Can_Merge_Deleted_Dtos_With_Delta()
    {
        var mockNodeA = new NodeId("mock-or-set-node-a");
        var mockNodeB = new NodeId("mock-or-set-node-b");

        var dtoB = GetRandomDto(mockNodeB, 3);

        var localItems = GetRandomItems(mockNodeA, 4);

        foreach (var item in localItems)
        {
            await _managedCrdt.AddAsync(item.Value);
        }

        var resultB0 = await _managedCrdt.MergeAsync(dtoB, CancellationToken.None);

        // Delete Last Value
        dtoB.VersionVectors![mockNodeB] += 1;
        var deletedItem = dtoB.Items!.Last();

        dtoB.Items!.ExceptWith(new[] { deletedItem });
        dtoB.Tombstones!.Add(deletedItem.Dot, new HashSet<NodeId> { mockNodeB });

        var delta = new MockManagedOptimizedObservedRemoveSet.MockUserSetDto
        {
            // NOTE: Missing Items, as in Delta ORSet Deleted Items would be represented by Tombstones
            Tombstones = dtoB.Tombstones,
            VersionVectors = dtoB.VersionVectors
        };

        var resultB1 = await _managedCrdt.MergeAsync(delta, CancellationToken.None);

        resultB0.Should().Be(MergeResult.ConflictSolved, "Merge of 3 Items with initial dtoB");
        resultB1.Should().Be(MergeResult.ConflictSolved, "Deletion of 1 Item");

        _managedCrdt.Value.Should().BeEquivalentTo(dtoB.Items!.Union(localItems).Select(i => i.Value).ToHashSet(),
            because: "Delta change with No Items but deleted Item's Tombstone");
    }

    private static MockManagedOptimizedObservedRemoveSet.MockUserSetDto GetRandomDto(NodeId node, uint items)
    {
        return new MockManagedOptimizedObservedRemoveSet.MockUserSetDto
        {
            VersionVectors = new Dictionary<NodeId, uint>
            {
                { node, items }
            },
            Items = GetRandomItems(node, items),
            Tombstones = new Dictionary<Dot<NodeId>, HashSet<NodeId>>()
        };
    }

    private static HashSet<DottedItem<NodeId, MockUser>> GetRandomItems(NodeId node, uint count)
    {
        return Enumerable.Range(0, (int) count).Select(i =>
            new DottedItem<NodeId, MockUser>(new Dot<NodeId>(node, (uint) i),
                new MockUser(Guid.NewGuid(), "mock-user-" + _userCount++))).ToHashSet();
    }
}
