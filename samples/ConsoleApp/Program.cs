using System.Threading.Tasks;
using Grpc.Net.Client;
using Nyris.Crdt.Distributed;
using ProtoBuf.Grpc.Client;

namespace ConsoleApp
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("http://localhost:4999");

            var client = channel.CreateGrpcService<IManagedCrdtService>();
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
