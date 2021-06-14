using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Nyris.Crdt;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Sets;
using ProtoBuf.Grpc.Client;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("http://localhost:4999");

            var client = channel.CreateGrpcService<IDtoPassingService>();
        }

        private static async IAsyncEnumerable<WithId<ManagedCrdtRegistry<NodeId, NodeId,
            ManagedGrowthSet, ManagedGrowthSet, HashSet<int>,
            List<int>, GrowthSetFactory>.RegistryDto>> GetEnumerable()
        {
            yield return new WithId<ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>,
                List<int>, GrowthSetFactory>.RegistryDto>
            {
                Id = "whatever",
                Dto = new ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>,
                    List<int>, GrowthSetFactory>.RegistryDto
                    {
                        Dict = new Dictionary<NodeId, List<int>>(),
                        Keys = new OptimizedObservedRemoveSet<NodeId, NodeId>.Dto
                        {
                            Items = new HashSet<VersionedSignedItem<NodeId, NodeId>>(),
                            ObservedState = new Dictionary<NodeId, uint>()
                        }
                    }
            };
        }
    }
}