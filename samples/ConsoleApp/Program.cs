using System;
using System.Collections.Generic;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Strategies.PartialReplication;

namespace ConsoleApp
{
    internal sealed class FakeReplicationStrategy : IPartialReplicationStrategy
    {
        /// <inheritdoc />
        public IDictionary<TKey, IList<NodeInfo>> GetDistribution<TKey>(IReadOnlyDictionary<TKey, ulong> collectionSizes,
            IEnumerable<NodeInfo> nodes) where TKey : IEquatable<TKey>, IComparable<TKey>
        {
            throw new NotImplementedException();
        }
    }

    internal static class Program
    {
        public static void Main(string[] args)
        {
            // using var channel = GrpcChannel.ForAddress("http://localhost:4999");

            var collection1 = new ImageInfoLwwCollectionWithSerializableOperations("0");
            var collection2 = new ImageInfoLwwCollectionWithSerializableOperations("0");

            collection1.TrySet(ImageGuid.Parse("fbcacccc-77d8-45f3-a823-10b705b34692"),
                new ImageInfo(new Uri("https://any.com"), "AABB"),
                DateTime.Parse("2021-01-01"),
                out _);
            collection1.TrySet(ImageGuid.Parse("fbcacccc-77d8-45f3-a823-10b705b34691"),
                new ImageInfo(new Uri("https://any.com"), "DDDD"),
                DateTime.Parse("2021-01-02"),
                out _);

            collection2.TrySet(ImageGuid.Parse("fbcacccc-77d8-45f3-a823-10b705b34692"),
                new ImageInfo(new Uri("https://any.com"), "AABB"),
                DateTime.Parse("2021-01-01"),
                out _);
            collection2.TrySet(ImageGuid.Parse("fbcacccc-77d8-45f3-a823-10b705b34691"),
                new ImageInfo(new Uri("https://any.com"), "DDDD"),
                DateTime.Parse("2021-01-02"),
                out _);
            //
            // var hash1 = collection1.CalculateHash();
            // var hash2 = collection2.CalculateHash();


        }

        // private static async IAsyncEnumerable<WithId<ManagedCrdtRegistry<NodeId, NodeId,
        //     ManagedGrowthSet, ManagedGrowthSet, HashSet<int>,
        //     List<int>, GrowthSetFactory>.RegistryDto>> GetEnumerable()
        // {
        //     yield return new WithId<ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>,
        //         List<int>, GrowthSetFactory>.RegistryDto>
        //     {
        //         Id = "whatever",
        //         Value = new ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>,
        //             List<int>, GrowthSetFactory>.RegistryDto
        //             {
        //                 Dict = new Dictionary<NodeId, WithId<List<int>>>(),
        //                 Keys = new OptimizedObservedRemoveSet<NodeId, NodeId>.OptimizedObservedRemoveSetDto
        //                 {
        //                     Items = new HashSet<VersionedSignedItem<NodeId, NodeId>>(),
        //                     ObservedState = new Dictionary<NodeId, uint>()
        //                 }
        //             }
        //     };
        // }
    }
}
