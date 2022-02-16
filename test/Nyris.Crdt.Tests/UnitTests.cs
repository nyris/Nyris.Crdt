using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using Xunit;

namespace Nyris.Crdt.Tests;

public class UnitTests
{
    [Fact]
    public async Task PartialReplicationRegistryWorks()
    {
        var nodes = await PrepareNodeMocksAsync(3);

        var registries = Enumerable.Range(0, 3)
            .Select(i => new PartiallyReplicatedImageInfoCollectionsRegistry("test-pr-reg")
            {
                ManagedCrdtContext = nodes[i].Context
            })
            .ToList();

        var collectionId = CollectionId.Parse("0bcacccc-77d8-45f3-a823-10b705b34692");
        await registries[0].TryAddCollectionAsync(collectionId, "0");
        registries[0].CalculateHash().SequenceEqual(registries[1].CalculateHash()).Should().BeFalse();

        await SyncCrdtsAsync<PartiallyReplicatedImageInfoCollectionsRegistry,
            PartiallyReplicatedImageInfoCollectionsRegistry.PartiallyReplicatedCrdtRegistryDto,
            PartiallyReplicatedImageInfoCollectionsRegistry.PartiallyReplicatedImageInfoCollectionsRegistryFactory>(registries,
            PartiallyReplicatedImageInfoCollectionsRegistry.DefaultFactory);

        for (var i = 0; i < registries.Count - 1; ++i)
        {
            registries[i].CalculateHash().SequenceEqual(registries[i + 1].CalculateHash()).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CrdtRegistryWorks()
    {
        var nodes = await PrepareNodeMocksAsync(3);

        var registries = Enumerable.Range(0, 3)
            .Select(i => new ImageInfoCollectionsRegistry("test-pr-reg")
            {
                ManagedCrdtContext = nodes[i].Context
            })
            .ToList();

        var collectionId = CollectionId.Parse("0bcacccc-77d8-45f3-a823-10b705b34692");
        var collection = new ImageInfoLwwCollection("0");
        await registries[0].TryAddAsync(collectionId, nodes[0].Id, collection);
        registries[0].CalculateHash().SequenceEqual(registries[1].CalculateHash()).Should().BeFalse();

        await SyncCrdtsAsync<ImageInfoCollectionsRegistry,
            ImageInfoCollectionsRegistry.RegistryDto,
            ImageInfoCollectionsRegistry.RegistryFactory>(registries, ImageInfoCollectionsRegistry.DefaultFactory);

        for (var i = 0; i < registries.Count - 1; ++i)
        {
            registries[i].CalculateHash().SequenceEqual(registries[i + 1].CalculateHash()).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CollectionInfosRegistryWorks()
    {
        var registry1 = new HashableCrdtRegistry<NodeId, CollectionId, CollectionInfo,
            CollectionInfo.CollectionInfoDto,
            CollectionInfo.CollectionInfoFactory>();

        var registry2 = new HashableCrdtRegistry<NodeId, CollectionId, CollectionInfo,
            CollectionInfo.CollectionInfoDto,
            CollectionInfo.CollectionInfoFactory>();

        var collectionId1 = CollectionId.New();
        var node1 = NodeId.New();
        var collectionId2 = CollectionId.New();
        var node2 = NodeId.New();

        registry1.TryAdd(node1, collectionId1, new CollectionInfo { Name = "1", Size = 1 }).Should().BeTrue();
        registry2.TryAdd(node2, collectionId2, new CollectionInfo { Name = "2", Size = 2 }).Should().BeTrue();

        registry1.Merge(registry2.ToDto()).Should().Be(MergeResult.ConflictSolved);
        registry1[collectionId2].Name.Should().Be("2");
        registry1[collectionId2].Size.Should().Be(2);
        registry1[collectionId1].Name.Should().Be("1");
        registry1[collectionId1].Size.Should().Be(1);

        registry2.Remove(collectionId2);
        registry1.Merge(registry2.ToDto()).Should().Be(MergeResult.ConflictSolved);
        registry1.TryGetValue(collectionId2, out _).Should().BeFalse();
        registry1[collectionId1].Name.Should().Be("1");
        registry1[collectionId1].Size.Should().Be(1);
    }

    private async Task<IList<NodeMock>> PrepareNodeMocksAsync(int n)
    {
        var result = new List<NodeMock>(n);
        for (var i = 0; i < n; ++i)
        {
            var id = NodeId.New();
            var infoProvider = new ManualNodeInfoProvider(id, new NodeInfo(new Uri($"https://{id}.node"), id));
            var context = new TestContext();
            await context.Nodes.AddAsync(infoProvider.GetMyNodeInfo(), id);
            result.Add(new NodeMock(id, context, infoProvider));
        }

        for (var i = 0; i < n; ++i)
        {
            for (var j = i + 1; j < n; ++j)
            {
                var dtoI = await result[i].Context.Nodes.ToDtoAsync();
                var dtoJ = await result[j].Context.Nodes.ToDtoAsync();
                await result[i].Context.Nodes.MergeAsync(dtoJ);
                await result[j].Context.Nodes.MergeAsync(dtoI);
            }
        }

        return result;
    }

    private async Task SyncCrdtsAsync<TCrdt, TDto, TFactory>(IList<TCrdt> crdts, TFactory factory)
        where TCrdt : ManagedCRDT<TDto>
        where TFactory : IManagedCRDTFactory<TCrdt, TDto>
    {

        for (var i = 0; i < crdts.Count; ++i)
        {
            for (var j = i + 1; j < crdts.Count; ++j)
            {
                await foreach (var dto in crdts[i].EnumerateDtoBatchesAsync())
                {
                    await crdts[j].MergeAsync(dto);
                }

                await foreach (var dto in crdts[j].EnumerateDtoBatchesAsync())
                {
                    await crdts[i].MergeAsync(dto);
                }
            }
        }
    }
}